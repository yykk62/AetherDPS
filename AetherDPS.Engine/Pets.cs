using System.Buffers.Binary;
using System.Text.Json;
using AionDpsMeter.Services.PacketProcessing.Routing;

namespace AetherDPS.Engine;

/// <summary>Eine Weltkarte: Kacheln (Assets/maps/&lt;Key&gt;) und Umrechnung Welt → Pixel (<c>px = Offset + Scale · Welt</c>).</summary>
public sealed record PetMap(string Key, string Table, int MapId, int Tile, int Grid, double Scale, double OffsetX, double OffsetY)
{
    public double Size => Tile * Grid;
    public (double X, double Y) ToPixel(double worldX, double worldY) => (OffsetX + Scale * worldX, OffsetY + Scale * worldY);
}

/// <summary>Ein Pet aus dem Pet-Fenster: Name, Gattung, die Monster, die seine Seele droppen.</summary>
public sealed record PetInfo(int Id, string Name, string Species, IReadOnlyList<int> Npcs);

/// <summary>Spawnpunkt eines Pet-Monsters (Weltkoordinaten).</summary>
public sealed record PetSpawn(int Npc, double X, double Y);

/// <summary>
/// Pets: welche Monster welche Pet-Seele droppen und wo sie spawnen (GameData/pets, aus SkeeveAN/Aion-DPS-Meter, MIT,
/// dort aus dem Spielclient gelesen). Dazu aus dem Spielverkehr: die eigenen Pet-Stufen (Liste beim Login, Opcode 0x9000)
/// und die eigene Position (0x373C, kommt bei Skills und Korrekturen). Beides geprüft an einer EU-Aufnahme vom 14.10.2026.
/// </summary>
public sealed class Pets
{
    public const ushort OpPetList = 0x9000, OpPosition = 0x373C;
    public const int TopLevel = 3;

    /// <summary>Gattungen in der Reihenfolge des Pet-Fensters.</summary>
    public static readonly string[] SpeciesOrder = ["Intellect", "Feral", "Nature", "Trans", "Special"];

    private static readonly Dictionary<string, int> MapIds = new() { ["verteron"] = 1010, ["altgard"] = 1110, ["abyss"] = 20 };

    private readonly Lock gate = new();
    private readonly string stateFile;
    private readonly SelfIdentity self;
    private readonly ZoneTracker zone;
    private readonly MeterSettings settings;
    private readonly Dictionary<int, int> petOfNpc = [];
    private readonly Dictionary<string, Dictionary<int, List<(double X, double Y)>>> spawns = [];
    private readonly Dictionary<string, List<(string Region, int[] Pets)>> regions = [];
    private State state;

    public IReadOnlyList<PetMap> Maps { get; }
    public IReadOnlyList<PetInfo> All { get; }

    /// <summary>Neue Pet-Stufen (Pet-Liste beim Login).</summary>
    public event EventHandler? LevelsChanged;

    /// <summary>Neue eigene Position.</summary>
    public event EventHandler? PositionChanged;

    public Pets(PacketDispatchService dispatch, SelfIdentity self, ZoneTracker zone, MeterSettings settings, MeterOptions options, string? dataDir = null)
    {
        this.self = self;
        this.zone = zone;
        this.settings = settings;
        stateFile = Path.Combine(options.DataDir, "pet_stufen.json");
        dataDir ??= Path.Combine(AppContext.BaseDirectory, "GameData", "pets");
        (Maps, All) = Load(dataDir);
        state = LoadState(stateFile);

        var previous = dispatch.Observer;
        dispatch.Observer = (opcode, data) =>
        {
            previous?.Invoke(opcode, data);
            if (opcode == OpPetList) OnPetList(data);
            else if (opcode == OpPosition) OnPosition(data);
            else if (opcode == FieldBosses.OpList) OnMapOpen();
        };
    }

    private (List<PetMap>, List<PetInfo>) Load(string dir)
    {
        var maps = new List<PetMap>();
        var pets = new List<PetInfo>();
        if (!File.Exists(Path.Combine(dir, "pets.json"))) return (maps, pets);

        using (var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "maps.json"))))
            foreach (var m in doc.RootElement.GetProperty("maps").EnumerateObject())
            {
                var v = m.Value;
                maps.Add(new PetMap(m.Name, v.GetProperty("table").GetString()!, MapIds.GetValueOrDefault(m.Name),
                    v.GetProperty("tile").GetInt32(), v.GetProperty("grid").GetInt32(), v.GetProperty("scale").GetDouble(),
                    v.GetProperty("offsetX").GetDouble(), v.GetProperty("offsetY").GetDouble()));
            }

        using (var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "spawns.json"))))
            foreach (var t in doc.RootElement.EnumerateObject())
            {
                var table = spawns[t.Name] = [];
                foreach (var n in t.Value.EnumerateObject())
                    table[int.Parse(n.Name)] = n.Value.EnumerateArray().Select(p => (p[0].GetDouble(), p[1].GetDouble())).ToList();
            }

        using (var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "regions.json"))))
            foreach (var g in doc.RootElement.GetProperty("groups").EnumerateArray())
                regions[g.GetProperty("id").GetString()!] = g.GetProperty("regions").EnumerateArray()
                    .Select(r => (Localized(r.GetProperty("names")), r.GetProperty("pets").EnumerateArray().Select(p => p.GetInt32()).ToArray()))
                    .ToList();

        using (var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "pets.json"))))
        {
            var npcs = new Dictionary<int, List<int>>();
            foreach (var n in doc.RootElement.GetProperty("monsters").EnumerateObject())
            {
                int npc = int.Parse(n.Name), pet = n.Value.GetInt32();
                petOfNpc[npc] = pet;
                if (!npcs.TryGetValue(pet, out var list)) npcs[pet] = list = [];
                list.Add(npc);
            }
            foreach (var p in doc.RootElement.GetProperty("pets").EnumerateObject())
            {
                int id = int.Parse(p.Name);
                var name = Localized(p.Value.GetProperty("names"));
                if (name.StartsWith("ペット：", StringComparison.Ordinal)) name = name[4..];
                pets.Add(new PetInfo(id, name, p.Value.GetProperty("category").GetString() ?? "", npcs.GetValueOrDefault(id) ?? []));
            }
        }
        return (maps, pets);
    }

    private static string Localized(JsonElement names)
    {
        var lang = Loc.Language switch { "zh-Hans" or "zh-Hant" => "en", var l => l };
        return names.TryGetProperty(lang, out var v) && v.GetString() is { Length: > 0 } s ? s
            : names.TryGetProperty("en", out var en) ? en.GetString() ?? "" : "";
    }

    public PetInfo? Get(int petId) => All.FirstOrDefault(p => p.Id == petId);

    /// <summary>Pet, dessen Seele dieses Monster droppt.</summary>
    public int? PetOfNpc(int npc) => petOfNpc.TryGetValue(npc, out var pet) ? pet : null;

    /// <summary>Spawnpunkte aller Monster dieses Pets auf einer Karte.</summary>
    public IReadOnlyList<PetSpawn> Spawns(PetInfo pet, PetMap map)
    {
        if (!spawns.TryGetValue(map.Table, out var table)) return [];
        return pet.Npcs.SelectMany(n => table.TryGetValue(n, out var pts) ? pts.Select(p => new PetSpawn(n, p.X, p.Y)) : []).ToList();
    }

    /// <summary>Gebiete einer Karte, in denen das Pet vorkommt (Namen in der Sprache der Oberfläche).</summary>
    public IReadOnlyList<string> Regions(PetInfo pet, PetMap map) =>
        regions.TryGetValue(map.Key, out var list) ? list.Where(r => r.Pets.Contains(pet.Id)).Select(r => r.Region).ToList() : [];

    /// <summary>Hat das Pet Spawnpunkte auf irgendeiner der Karten?</summary>
    public bool HasSpawns(PetInfo pet) => Maps.Any(m => Spawns(pet, m).Count > 0);

    /// <summary>Eigene Stufe (0 = noch nicht gesammelt, null = Pet-Liste noch nie gesehen).</summary>
    public int? Level(int petId)
    {
        lock (gate)
        {
            if (Mine() is not { } mine) return null;
            return mine.Levels.GetValueOrDefault(petId);
        }
    }

    /// <summary>Fortschritt zur nächsten Stufe (gesammelte Seelen).</summary>
    public int Progress(int petId)
    {
        lock (gate) return Mine()?.Progress.GetValueOrDefault(petId) ?? 0;
    }

    /// <summary>Seelen, die bis zur nächsten Stufe nötig sind (laut Pet-Fenster: 25 bis Stufe 2, 75 bis Stufe 3).</summary>
    public static int Needed(int level) => level switch { 1 => 25, 2 => 75, _ => 0 };

    public bool KnowsLevels { get { lock (gate) return Mine() is not null; } }

    /// <summary>Letzte bekannte eigene Position (Welt) mit Karte und Zeit.</summary>
    public (double X, double Y, int MapId, DateTime At)? Position { get; private set; }

    /// <summary>Karte, auf der man laut Spiel gerade ist (auch in Ebenen wie 101001 von Verteron 1010).</summary>
    public PetMap? CurrentMap
    {
        get
        {
            int id = zone.MapId;
            return Maps.FirstOrDefault(m => m.MapId != 0 && (id == m.MapId || id / 100 == m.MapId));
        }
    }

    private void OnPetList(byte[] data)
    {
        if (ParsePetList(data) is not { } list) return;
        var name = settings.CharacterName;
        if (string.IsNullOrWhiteSpace(name)) return;
        lock (gate)
        {
            state.Characters[name] = new CharacterPets
            {
                Active = list.Active,
                Levels = list.Levels,
                Progress = list.Progress,
                SeenAt = DateTime.Now,
            };
        }
        Save();
        LevelsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnPosition(byte[] data)
    {
        if (self.SelfId is not int selfId || ParsePosition(data) is not { } p || p.Entity != selfId) return;
        Position = (p.X, p.Y, zone.MapId, DateTime.Now);
        PositionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Die Spielkarte ist offen: Solange Karte oder Feldboss-Liste im Spiel offen sind, schickt der Server etwa
    /// jede Sekunde die Feldboss-Liste (0x9101). Bleibt sie ein paar Sekunden aus, ist die Karte zu.
    /// </summary>
    public DateTime? MapOpenSeen { get; private set; }

    public event EventHandler? MapOpen;

    private void OnMapOpen()
    {
        MapOpenSeen = DateTime.Now;
        MapOpen?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Ein Spawnpunkt in der Nähe: Pet, Entfernung in Metern, Himmelsrichtung (0 = Nord, im Uhrzeigersinn).</summary>
    public sealed record Nearby(PetInfo Pet, PetSpawn Spawn, double Meters, double Bearing);

    /// <summary>Nächster Spawnpunkt je Pet (nur gewünschte Pets), sortiert nach Entfernung.</summary>
    public IReadOnlyList<Nearby> Nearest(PetMap map, double x, double y, Func<PetInfo, bool> wanted, int count)
    {
        var list = new List<Nearby>();
        foreach (var pet in All.Where(wanted))
        {
            Nearby? best = null;
            foreach (var s in Spawns(pet, map))
            {
                double dx = s.X - x, dy = s.Y - y;
                double meters = Math.Sqrt(dx * dx + dy * dy) / 100;   // Weltkoordinaten in cm
                if (best is null || meters < best.Meters)
                    best = new Nearby(pet, s, meters, (Math.Atan2(dx, -dy) * 180 / Math.PI + 360) % 360);   // +y = Süden (Karte nach unten)
            }
            if (best is not null) list.Add(best);
        }
        return list.OrderBy(n => n.Meters).Take(count).ToList();
    }

    /// <summary>Himmelsrichtung als Kürzel (N, NO, O …) aus dem Winkel.</summary>
    public static string Compass(double bearing) => Loc.T("compass." + (int)Math.Round(bearing / 45) % 8);

    public sealed record PetList(int Active, Dictionary<int, int> Levels, Dictionary<int, int> Progress);

    /// <summary>
    /// Pet-Liste (beim Login): <c>len | 00 90 | aktives Pet u32 | varint Anzahl | n × (id u32, id u32, Stufe u32) | varint m | m × (id u32, Fortschritt u32)</c>.
    /// Aufbau aus SkeeveAN (MIT), an einer EU-Aufnahme bestätigt (49 Pets, aktives Pet = Pet im NCSOFT-Profil).
    /// </summary>
    public static PetList? ParsePetList(byte[] data)
    {
        int pos = SkipLength(data);
        var f = data.AsSpan(pos);
        if (f.Length < 8) return null;
        int p = 6;
        if (!TryVarint(f, ref p, out long count) || count is < 1 or > 2000 || p + count * 12 > f.Length) return null;
        var levels = new Dictionary<int, int>();
        for (long i = 0; i < count; i++, p += 12)
        {
            uint id = BinaryPrimitives.ReadUInt32LittleEndian(f[p..]);
            if (id != BinaryPrimitives.ReadUInt32LittleEndian(f[(p + 4)..]) || id is < 1000 or > 99999) return null;
            uint level = BinaryPrimitives.ReadUInt32LittleEndian(f[(p + 8)..]);
            if (level > 10) return null;
            levels[(int)id] = (int)level;
        }
        var progress = new Dictionary<int, int>();
        if (TryVarint(f, ref p, out long m) && m is >= 0 and <= 2000 && p + m * 8 <= f.Length)
            for (long i = 0; i < m; i++, p += 8)
                progress[(int)BinaryPrimitives.ReadUInt32LittleEndian(f[p..])] = (int)BinaryPrimitives.ReadUInt32LittleEndian(f[(p + 4)..]);
        uint active = BinaryPrimitives.ReadUInt32LittleEndian(f[2..]);
        return new PetList(active is >= 1000 and <= 99999 ? (int)active : 0, levels, progress);
    }

    /// <summary>Position einer Einheit: <c>len | 3C 37 | Einheit (varint) | 03 | x f32 | y f32 | …</c>.</summary>
    public static (long Entity, double X, double Y)? ParsePosition(byte[] data)
    {
        int pos = SkipLength(data);
        var f = data.AsSpan(pos);
        int p = 2;
        if (!TryVarint(f, ref p, out long entity) || f.Length < p + 9 || f[p] != 3) return null;
        float x = BinaryPrimitives.ReadSingleLittleEndian(f[(p + 1)..]);
        float y = BinaryPrimitives.ReadSingleLittleEndian(f[(p + 5)..]);
        if (!float.IsFinite(x) || !float.IsFinite(y) || Math.Abs(x) > 1_000_000 || Math.Abs(y) > 1_000_000) return null;
        return (entity, x, y);
    }

    private static int SkipLength(byte[] data)
    {
        int pos = 0;
        while (pos < data.Length && (data[pos] & 0x80) != 0) pos++;
        return pos + 1;
    }

    private static bool TryVarint(ReadOnlySpan<byte> b, ref int p, out long value)
    {
        value = 0;
        for (int shift = 0; shift < 35; shift += 7)
        {
            if (p >= b.Length) return false;
            byte c = b[p++];
            value |= (long)(c & 0x7F) << shift;
            if ((c & 0x80) == 0) return true;
        }
        return false;
    }

    private CharacterPets? Mine() =>
        settings.CharacterName is { } name && state.Characters.TryGetValue(name, out var mine) ? mine : null;

    private sealed class State
    {
        public Dictionary<string, CharacterPets> Characters { get; set; } = [];
    }

    private sealed class CharacterPets
    {
        public int Active { get; set; }
        public Dictionary<int, int> Levels { get; set; } = [];
        public Dictionary<int, int> Progress { get; set; } = [];
        public DateTime SeenAt { get; set; }
    }

    private static State LoadState(string path)
    {
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize<State>(File.ReadAllText(path)) ?? new() : new();
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return new();
        }
    }

    private void Save()
    {
        try
        {
            string json;
            lock (gate) json = JsonSerializer.Serialize(state);
            File.WriteAllText(stateFile, json);
        }
        catch (IOException) { }
    }
}
