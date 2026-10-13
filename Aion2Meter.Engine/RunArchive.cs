using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Aion2Meter.Engine;

public sealed record ArchivedPlayer(long PlayerId, string Name, bool IsUser, int ClassId, long Damage, double Dps,
    double Share, int Deaths, double ActivityPercent, List<RunSkill> Skills, List<RunBuff> Buffs);

public sealed record ArchivedRun(
    string Id, DateTime StartedAt, double DurationSeconds, double CombatSeconds, string Name,
    long TotalDamage, double PartyDps, List<ArchivedPlayer> Players, List<RunBoss> Bosses)
{
    public TimeSpan Duration => TimeSpan.FromSeconds(DurationSeconds);
    public int BossesKilled => Bosses.Count(b => b.Killed);
}

/// <summary>
/// Speichert jeden Lauf (STRG+W … STRG+W) als JSON in &lt;Datenordner&gt;\Laeufe.
/// Spielerliste wie im Overlay: Beschwörungen zugeschlagen, im Gruppenmodus nur die Gruppe.
/// Bestzeit = kürzester Lauf mit demselben Namen (Name = letzter getöteter Boss).
/// </summary>
public sealed class RunArchive
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly string folder;
    private readonly RunTracker tracker;
    private readonly PartyRoster roster;
    private readonly MeterSettings settings;
    private readonly Lock gate = new();
    private List<ArchivedRun>? runs;

    public event EventHandler? Changed;

    public RunArchive(MeterOptions options, RunTracker tracker, PartyRoster roster, MeterSettings settings)
    {
        folder = Path.Combine(options.DataDir, "Laeufe");
        this.tracker = tracker;
        this.roster = roster;
        this.settings = settings;
        tracker.Completed += snapshot =>
        {
            var run = Build(snapshot);
            if (run.Players.Count > 0) Save(run);
        };
    }

    /// <summary>Lauf aus einem Schnappschuss bauen (auch für den laufenden Lauf).</summary>
    public ArchivedRun Build(RunSnapshot snapshot)
    {
        var lines = snapshot.Players
            .Select(p => new MeterLine(p.PlayerId, p.Name, p.IsUser, p.IsIdentified, p.ClassId, p.ClassName, p.Damage, p.Dps, p.Share))
            .ToList();
        var merge = MeterFilter.Apply(lines, roster, settings);

        var byId = snapshot.Players.ToDictionary(p => (long)p.PlayerId);
        var players = merge.Lines
            .Where(l => l.IsIdentified || l.DamagePercentage > 1)
            .Select(l =>
            {
                var raw = byId.GetValueOrDefault(l.PlayerId);
                return new ArchivedPlayer(l.PlayerId, l.PlayerName, l.IsUser, l.ClassId, l.TotalDamage, l.DamagePerSecond,
                    l.DamagePercentage, raw?.Deaths ?? 0, raw?.ActivityPercent ?? 0,
                    tracker.Skills((int)l.PlayerId, merge.MergedInto.GetValueOrDefault(l.PlayerId)).Take(12).ToList(),
                    tracker.Buffs((int)l.PlayerId).Take(10).ToList());
            })
            .ToList();

        var lastKill = snapshot.Bosses.LastOrDefault(b => b.Killed) ?? snapshot.Bosses.LastOrDefault();
        return new ArchivedRun(
            snapshot.StartedAt.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture),
            snapshot.StartedAt, snapshot.Elapsed.TotalSeconds, snapshot.CombatTime.TotalSeconds,
            lastKill?.Name ?? "Lauf", players.Sum(p => p.Damage), players.Sum(p => p.Dps), players,
            snapshot.Bosses.ToList());
    }

    public IReadOnlyList<ArchivedRun> All()
    {
        lock (gate)
        {
            runs ??= Load();
            return runs.OrderByDescending(r => r.StartedAt).ToList();
        }
    }

    /// <summary>Kürzester Lauf mit demselben Namen und mindestens einem getöteten Boss.</summary>
    public ArchivedRun? BestOf(string name) =>
        All().Where(r => r.Name == name && r.BossesKilled > 0).MinBy(r => r.DurationSeconds);

    /// <summary>Deine Ø-DPS in früheren Läufen mit demselben Namen (ohne den angegebenen).</summary>
    public double? AverageUserDps(string name, string? excludeId = null)
    {
        var values = All().Where(r => r.Name == name && r.Id != excludeId)
            .Select(r => r.Players.FirstOrDefault(p => p.IsUser)?.Dps)
            .OfType<double>().ToList();
        return values.Count > 0 ? values.Average() : null;
    }

    public void Delete(string id)
    {
        lock (gate)
        {
            var file = Path.Combine(folder, $"{id}.json");
            if (File.Exists(file)) File.Delete(file);
            runs?.RemoveAll(r => r.Id == id);
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Save(ArchivedRun run)
    {
        lock (gate)
        {
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, $"{run.Id}.json"), JsonSerializer.Serialize(run, Json));
            runs ??= Load();
            runs.RemoveAll(r => r.Id == run.Id);
            runs.Add(run);
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private List<ArchivedRun> Load()
    {
        if (!Directory.Exists(folder)) return [];
        var list = new List<ArchivedRun>();
        foreach (var file in Directory.GetFiles(folder, "*.json"))
        {
            try
            {
                if (JsonSerializer.Deserialize<ArchivedRun>(File.ReadAllText(file), Json) is { } run) list.Add(run);
            }
            catch (JsonException)
            {
                // kaputte Datei überspringen
            }
        }
        return list;
    }

    /// <summary>Text fürs Teilen (Discord: Code-Block, damit die Spalten stehen bleiben).</summary>
    public static string ToDiscordText(ArchivedRun run, Func<int, string, string> className)
    {
        var de = CultureInfo.GetCultureInfo("de-DE");
        var sb = new StringBuilder();
        sb.AppendLine($"**{run.Name}** – {run.StartedAt:dd.MM.yyyy HH:mm} – Dauer {Fmt(run.Duration)}");
        sb.AppendLine("```");
        sb.AppendLine($"{"#",-2} {"Spieler",-16} {"Klasse",-13} {"DPS",9} {"Schaden",12} {"Anteil",7}");
        int rank = 1;
        foreach (var p in run.Players)
        {
            var name = p.Name.StartsWith("Player_", StringComparison.Ordinal) ? "Unbekannt" : p.Name;
            sb.AppendLine($"{rank++,-2} {Cut(name, 16),-16} {Cut(className(p.ClassId, ""), 13),-13} " +
                          $"{p.Dps.ToString("N0", de),9} {p.Damage.ToString("N0", de),12} {p.Share.ToString("0.0", de),6}%");
        }
        if (run.Bosses.Count > 0)
        {
            sb.AppendLine();
            foreach (var b in run.Bosses)
                sb.AppendLine($"{(b.Killed ? "✔" : "✘")} {Cut(b.Name, 28),-28} {Fmt(b.Duration)}");
        }
        sb.AppendLine("```");
        sb.Append("_AION 2 Meter_");
        return sb.ToString();

        static string Cut(string s, int n) => s.Length <= n ? s : s[..(n - 1)] + "…";
    }

    public static string Fmt(TimeSpan t) => t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"mm\:ss");
}
