using System.Text.Encodings.Web;
using System.Text.Json;

namespace AetherDPS.Engine.Community;

/// <summary>Ein Messpunkt im Verlauf des eigenen Charakters.</summary>
public sealed record ProfileSnapshot(DateTime At, int CombatPower, int ItemLevel);

/// <summary>Ein Ausrüstungsteil für die Karte auf der Startseite (nur das Nötigste).</summary>
public sealed record ProfileGear(string Slot, string? Icon, string Grade, int Enchant);

/// <summary>Letzter bekannter Stand des eigenen Charakters (für die Startseite, auch ohne Verbindung).</summary>
public sealed record ProfileLatest(string Name, string Server, string ClassName, int ClassId, string? Portrait,
    int CombatPower, int ItemLevel, double AverageEnchant, List<ProfileGear> Gear, DateTime At);

/// <summary>
/// Verlauf von Kampfkraft und Itemstufe des eigenen Charakters, lokal in &lt;Datenordner&gt;\profil_verlauf.json.
/// Gefüllt, wenn das eigene Profil geladen wird (Reiter Profil oder der Abruf alle paar Stunden, siehe
/// <see cref="ProfileWatcher"/>). Es verlässt nichts den PC; NCSOFT wird nur wie beim Öffnen des Profils gefragt.
/// </summary>
public sealed class ProfileHistory
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private const int MaxPoints = 3000;

    private readonly string file;
    private readonly Lock gate = new();
    private State state;

    public event EventHandler? Changed;

    public ProfileHistory(MeterOptions options) : this(Path.Combine(options.DataDir, "profil_verlauf.json")) { }

    public ProfileHistory(string file)
    {
        this.file = file;
        state = Load();
    }

    /// <summary>Messpunkte des angegebenen Charakters, älteste zuerst.</summary>
    public IReadOnlyList<ProfileSnapshot> Points(string name, int serverId)
    {
        lock (gate)
            return state.Characters.TryGetValue(Key(name, serverId), out var c) ? c.Points.ToList() : [];
    }

    public ProfileLatest? Latest(string name, int serverId)
    {
        lock (gate)
            return state.Characters.TryGetValue(Key(name, serverId), out var c) ? c.Latest : null;
    }

    /// <summary>Profil festhalten. Gleiche Werte innerhalb einer Stunde ergeben keinen neuen Punkt.</summary>
    public void Add(ArmoryProfile p, DateTime? at = null)
    {
        var now = at ?? DateTime.Now;
        lock (gate)
        {
            var key = Key(p.Name, p.ServerId);
            if (!state.Characters.TryGetValue(key, out var c)) state.Characters[key] = c = new CharacterState();
            c.Latest = new ProfileLatest(p.Name, p.Server, p.ClassName, p.ClassId, p.Portrait, p.CombatPower, p.ItemLevel, p.AverageEnchant,
                p.Equipment.Select(e => new ProfileGear(e.Slot, e.Icon, e.Grade, e.Enchant)).ToList(), now);
            var last = c.Points.LastOrDefault();
            bool same = last is not null && last.CombatPower == p.CombatPower && last.ItemLevel == p.ItemLevel;
            if (!(same && now - last!.At < TimeSpan.FromHours(1)))
            {
                c.Points.Add(new ProfileSnapshot(now, p.CombatPower, p.ItemLevel));
                if (c.Points.Count > MaxPoints) c.Points.RemoveRange(0, c.Points.Count - MaxPoints);
            }
            Save();
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static string Key(string name, int serverId) => $"{serverId}|{name.Trim()}".ToLowerInvariant();

    private State Load()
    {
        try
        {
            if (File.Exists(file)) return JsonSerializer.Deserialize<State>(File.ReadAllText(file), Json) ?? new State();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { }
        return new State();
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, JsonSerializer.Serialize(state, Json));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    private sealed class State
    {
        public Dictionary<string, CharacterState> Characters { get; set; } = [];
    }

    private sealed class CharacterState
    {
        public List<ProfileSnapshot> Points { get; set; } = [];
        public ProfileLatest? Latest { get; set; }
    }
}

/// <summary>
/// Ruft das eigene Profil (Name + Server aus den Einstellungen) kurz nach dem Start und danach alle
/// <see cref="Interval"/> bei NCSOFT ab und legt es im <see cref="ProfileHistory"/> ab – nur solange der Meter
/// läuft und nur, wenn <see cref="MeterSettings.ProfileHistory"/> an ist (abschaltbar in den Einstellungen).
/// </summary>
public sealed class ProfileWatcher(Armory armory, ProfileHistory history, MeterSettings settings) : IDisposable
{
    public static readonly TimeSpan Interval = TimeSpan.FromHours(4);
    private Timer? timer;

    public void Start(TimeSpan? firstAfter = null) =>
        timer ??= new Timer(_ => _ = TickAsync(), null, firstAfter ?? TimeSpan.FromSeconds(45), Interval);

    public async Task TickAsync()
    {
        if (!settings.ProfileHistory || string.IsNullOrWhiteSpace(settings.CharacterName)) return;
        if (Armory.ServerId(settings.CharacterServer) is not int server) return;
        var latest = history.Latest(settings.CharacterName!, server);
        if (latest is not null && DateTime.Now - latest.At < Interval - TimeSpan.FromMinutes(5)) return;   // kürzlich schon
        if (await armory.LoadAsync(settings.CharacterName!, server, fresh: true) is { } profile) history.Add(profile);   // frisch, nicht aus dem Zwischenspeicher
    }

    public void Dispose() => timer?.Dispose();
}
