using System.Buffers.Binary;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using AionDpsMeter.Services.PacketProcessing.Routing;
using AionDpsMeter.Services.Services.Entity;

namespace AetherDPS.Engine;

/// <summary>Fester Zeitplan eines Abyss-Bosses (Europe/Berlin = Ortszeit der EU-Server).</summary>
public sealed class FixedSpawn
{
    [JsonPropertyName("kind")] public string Kind { get; set; } = "";
    [JsonPropertyName("days")] public List<int>? Days { get; set; }
    [JsonPropertyName("at")] public string? At { get; set; }
    [JsonPropertyName("base")] public string? Base { get; set; }
    [JsonPropertyName("everyMinutes")] public int EveryMinutes { get; set; }
    [JsonPropertyName("openMinutes")] public int OpenMinutes { get; set; }

    /// <summary>Nächster Termin ab <paramref name="now"/> (Ortszeit).</summary>
    public DateTime? Next(DateTime now)
    {
        if (Kind == "weekly" && Days is { Count: > 0 } && TimeSpan.TryParse(At, out var at))
        {
            for (int d = 0; d <= 7; d++)
            {
                var day = now.Date.AddDays(d);
                if (Days.Contains((int)day.DayOfWeek) && day + at > now.AddMinutes(-OpenMinutes)) return day + at;
            }
        }
        if (Kind == "cycle" && EveryMinutes > 0 && TimeSpan.TryParse(Base, out var b))
            return TimerService.NextRift(now.AddMinutes(-OpenMinutes), b.ToString(@"hh\:mm"), EveryMinutes / 60.0);
        return null;
    }
}

public sealed class FieldBossInfo
{
    [JsonPropertyName("slot")] public int Slot { get; set; }
    [JsonPropertyName("npc")] public int Npc { get; set; }
    [JsonPropertyName("x")] public double X { get; set; }
    [JsonPropertyName("y")] public double Y { get; set; }
    /// <summary>Respawn nach dem Kill in Minuten (gemessen bzw. Schätzung), null = unbekannt.</summary>
    [JsonPropertyName("respawn")] public int? Respawn { get; set; }
    [JsonPropertyName("fixed")] public FixedSpawn? Fixed { get; set; }

    [JsonIgnore] public FieldBossMap Map { get; set; } = null!;
    [JsonIgnore] public string Name => GameText.NpcName(Npc) ?? $"#{Npc}";
}

public sealed class FieldBossMap
{
    [JsonPropertyName("map")] public int Id { get; set; }
    [JsonPropertyName("key")] public string Key { get; set; } = "";
    [JsonPropertyName("names")] public Dictionary<string, string> Names { get; set; } = [];
    [JsonPropertyName("bosses")] public List<FieldBossInfo> Bosses { get; set; } = [];

    public string Name => Names.GetValueOrDefault(GameText.Language) ?? Names.GetValueOrDefault("en") ?? $"#{Id}";
}

/// <summary>Was der Meter über einen Boss weiß (pro Server gespeichert).</summary>
public sealed class FieldBossState
{
    public bool Alive { get; set; }
    /// <summary>Lebt: seit wann; tot: Respawn-Zeit. Null = Server nennt keine Zeit.</summary>
    public DateTime? Time { get; set; }
    public DateTime SeenAt { get; set; }
    /// <summary>Zeit nur geschätzt (Kill gesehen, Liste noch nicht wieder geöffnet).</summary>
    public bool Estimated { get; set; }
    public DateTime? KilledAt { get; set; }
}

public enum FieldBossStatus { Unknown, Alive, Respawning, ProbablyUp, Scheduled }

public sealed record FieldBossView(FieldBossInfo Boss, FieldBossStatus Status, DateTime? Time, bool Estimated, bool Favorite);

/// <summary>
/// Feldbosse mit den echten Zeiten des Servers. Öffnet man im Spiel die Karte, schickt der Server
/// jede Sekunde die Liste der Feldbosse dieser Karte (0x9101, Bytes „01 91“):
/// <c>u16 0 | Karte u32 | Anzahl u8 | n × (lebt u8 | Slot varint | [lebt: x y z f32] | [u8?] | Zeit i64 ms)</c>.
/// Zeit = seit wann er lebt bzw. wann er wiederkommt. Layout wie in TheLoop705/aion2dps, cyberbadger,
/// nathantek und Waffle (geprüft an einer EU-Aufnahme, Verteron 24 Bosse). Ohne offene Karte bleibt der
/// letzte Stand gespeichert; ein beobachteter Kill schätzt den Respawn über die bekannte Dauer.
/// </summary>
public sealed class FieldBosses
{
    public const ushort OpList = 0x9101;

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly MeterSettings settings;
    private readonly string stateFile;
    private readonly Lock gate = new();
    private readonly Dictionary<int, FieldBossInfo> bySlot = [];
    private readonly Dictionary<int, FieldBossInfo> byNpc = [];
    private StateFile state;

    public IReadOnlyList<FieldBossMap> Maps { get; }

    /// <summary>Neue Daten (Liste gelesen oder Kill gesehen).</summary>
    public event EventHandler? Changed;

    public FieldBosses(PacketDispatchService dispatch, EntityTracker entities, MeterSettings settings, MeterOptions options, string? dataFile = null)
    {
        this.settings = settings;
        stateFile = Path.Combine(options.DataDir, "feldbosse.json");
        Maps = LoadMaps(dataFile ?? Path.Combine(AppContext.BaseDirectory, "GameData", "field_bosses.json"));
        foreach (var map in Maps)
            foreach (var boss in map.Bosses)
            {
                boss.Map = map;
                bySlot[boss.Slot] = boss;
                byNpc.TryAdd(boss.Npc, boss);
            }
        state = LoadState(stateFile);

        var previous = dispatch.Observer;
        dispatch.Observer = (opcode, data) =>
        {
            previous?.Invoke(opcode, data);
            if (opcode == OpList) OnList(data);
        };
        entities.EntityDied += id =>
        {
            if (entities.GetTargetMob(id) is { MobCode: > 0 } mob) OnKilled(mob.MobCode, DateTime.Now);
        };
    }

    public bool IsFieldBoss(int npc) => byNpc.ContainsKey(npc);

    public IReadOnlySet<int> Favorites { get { lock (gate) return state.Favorites.ToHashSet(); } }

    public void SetFavorite(int npc, bool favorite)
    {
        lock (gate)
        {
            if (favorite) state.Favorites.Add(npc);
            else state.Favorites.Remove(npc);
        }
        Save();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Wurde die Liste (Karte im Spiel) für diesen Server schon einmal gelesen?</summary>
    public bool HasData { get { lock (gate) return Server().Count > 0; } }

    public IReadOnlyList<FieldBossView> Views(DateTime? at = null)
    {
        var now = at ?? DateTime.Now;
        var list = new List<FieldBossView>();
        lock (gate)
        {
            var server = Server();
            foreach (var boss in Maps.SelectMany(m => m.Bosses))
            {
                bool fav = state.Favorites.Contains(boss.Npc);
                server.TryGetValue(boss.Slot, out var s);
                list.Add(Describe(boss, s, now, fav));
            }
        }
        return list;
    }

    private static FieldBossView Describe(FieldBossInfo boss, FieldBossState? s, DateTime now, bool fav)
    {
        if (s is { Alive: true }) return new(boss, FieldBossStatus.Alive, s.Time, false, fav);
        if (s is { Time: DateTime t })
            return t > now ? new(boss, FieldBossStatus.Respawning, t, s.Estimated, fav)
                           : new(boss, FieldBossStatus.ProbablyUp, t, s.Estimated, fav);
        if (boss.Fixed?.Next(now) is DateTime next) return new(boss, FieldBossStatus.Scheduled, next, false, fav);
        return new(boss, FieldBossStatus.Unknown, null, false, fav);
    }

    private void OnKilled(int npc, DateTime now)
    {
        if (!byNpc.TryGetValue(npc, out var boss)) return;
        lock (gate)
        {
            var server = Server();
            int? minutes = state.Learned.TryGetValue(boss.Slot, out var learned) ? learned : boss.Respawn;
            server[boss.Slot] = new FieldBossState
            {
                Alive = false,
                Time = minutes is int m ? now.AddMinutes(m) : null,
                SeenAt = now,
                Estimated = true,
                KilledAt = now,
            };
        }
        Save();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnList(byte[] data)
    {
        if (Parse(data, bySlot.ContainsKey) is not { } list) return;
        var now = DateTime.Now;
        bool changed = false;
        lock (gate)
        {
            var server = Server();
            foreach (var e in list.Entries)
            {
                if (!bySlot.ContainsKey(e.Slot)) continue;
                DateTime? time = e.TimeMs > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(e.TimeMs).LocalDateTime : null;
                server.TryGetValue(e.Slot, out var old);
                // Dauer lernen: Kill gesehen, jetzt nennt der Server die Respawn-Zeit
                if (!e.Alive && time is DateTime respawn && old?.KilledAt is DateTime killed && respawn > killed)
                {
                    int minutes = (int)Math.Round((respawn - killed).TotalMinutes / 5) * 5;
                    if (minutes is >= 5 and <= 24 * 60) state.Learned[e.Slot] = minutes;
                }
                if (old is not null && old.Alive == e.Alive && old.Time == time && !old.Estimated)
                {
                    old.SeenAt = now;
                    continue;
                }
                server[e.Slot] = new FieldBossState
                {
                    Alive = e.Alive,
                    Time = time,
                    SeenAt = now,
                    KilledAt = !e.Alive ? old?.KilledAt ?? (old?.Alive == true ? now : null) : null,
                };
                changed = true;
            }
        }
        if (!changed) return;
        Save();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    // ---------- Paket ----------

    public sealed record ListEntry(int Slot, bool Alive, long TimeMs);
    public sealed record ListPacket(int Map, IReadOnlyList<ListEntry> Entries);

    /// <summary>Liest die Liste; null, wenn das Paket nicht passt. <paramref name="knownSlot"/> hilft beim Ausrichten.</summary>
    public static ListPacket? Parse(byte[] data, Func<int, bool>? knownSlot = null)
    {
        int pos = 0;
        while (pos < data.Length && (data[pos] & 0x80) != 0) pos++;
        pos += 1 + 2;   // Varint-Ende, Opcode
        var b = data.AsSpan(pos);
        if (b.Length < 7) return null;
        int map = BinaryPrimitives.ReadInt32LittleEndian(b[2..]);
        int count = b[6];
        int o = 7;
        var entries = new List<ListEntry>();
        for (int i = 0; i < count; i++)
        {
            if (!Header(b, o, map, knownSlot, out bool alive, out int slot, out int at)) break;
            if (alive) at += 12;   // x y z
            bool last = i == count - 1;
            bool found = false;
            for (int extra = 0; extra <= 1 && !found; extra++)
            {
                int t = at + extra;
                if (t + 8 > b.Length) break;
                long time = BinaryPrimitives.ReadInt64LittleEndian(b[t..]);
                if (time != 0 && time is < 1_600_000_000_000 or > 2_600_000_000_000) continue;
                int end = t + 8;
                bool fits = last ? b.Length - end <= 16 : Header(b, end, map, knownSlot, out _, out _, out _);
                if (!fits) continue;
                entries.Add(new ListEntry(slot, alive, time));
                o = end;
                found = true;
            }
            if (!found) break;
        }
        return entries.Count > 0 ? new ListPacket(map, entries) : null;
    }

    private static bool Header(ReadOnlySpan<byte> b, int o, int map, Func<int, bool>? knownSlot, out bool alive, out int slot, out int next)
    {
        alive = false;
        slot = 0;
        next = o;
        if (o >= b.Length || b[o] > 1) return false;
        alive = b[o] == 1;
        int shift = 0, p = o + 1;
        while (p < b.Length && shift < 35)
        {
            slot |= (b[p] & 0x7F) << shift;
            if ((b[p++] & 0x80) == 0)
            {
                next = p;
                // Slot = Karte × 100 + Platz; manche Karten (Eltnen, Morheim) tragen den NPC-Code selbst
                return (slot > map * 100 && slot < map * 100 + 100) || knownSlot?.Invoke(slot) == true;
            }
            shift += 7;
        }
        return false;
    }

    // ---------- Speichern ----------

    private sealed class StateFile
    {
        public Dictionary<string, Dictionary<int, FieldBossState>> Servers { get; set; } = [];
        public HashSet<int> Favorites { get; set; } = [];
        /// <summary>Selbst gemessene Respawn-Dauer pro Slot (Minuten).</summary>
        public Dictionary<int, int> Learned { get; set; } = [];
    }

    /// <summary>Die Liste gilt pro Server (eigener Charakter).</summary>
    private Dictionary<int, FieldBossState> Server()
    {
        var key = string.IsNullOrWhiteSpace(settings.CharacterServer) ? "?" : settings.CharacterServer;
        if (!state.Servers.TryGetValue(key, out var server)) state.Servers[key] = server = [];
        return server;
    }

    private static IReadOnlyList<FieldBossMap> LoadMaps(string file)
    {
        try
        {
            if (!File.Exists(file)) return [];
            using var doc = JsonDocument.Parse(File.ReadAllText(file));
            return doc.RootElement.GetProperty("maps").Deserialize<List<FieldBossMap>>() ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static StateFile LoadState(string file)
    {
        try
        {
            return File.Exists(file) ? JsonSerializer.Deserialize<StateFile>(File.ReadAllText(file)) ?? new() : new();
        }
        catch (JsonException)
        {
            return new();
        }
    }

    private void Save()
    {
        try
        {
            string json;
            lock (gate) json = JsonSerializer.Serialize(state, Json);
            File.WriteAllText(stateFile, json);
        }
        catch (IOException)
        {
            // nächstes Mal
        }
    }
}
