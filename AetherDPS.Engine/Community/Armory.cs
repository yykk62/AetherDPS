using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AetherDPS.Engine.Community;

public sealed record ArmoryItem(string Slot, string Name, string Grade, int Enchant, string? Icon, long Id = 0, int SlotPos = 0, int Exceed = 0);

public sealed record ArmorySkill(string Name, string Category, int Level, string? Icon);

/// <summary>Grundwert (Macht, Agilität …) oder Gottheiten-Wert (Gerechtigkeit [Nezekan] …) mit seinen Wirkungen.</summary>
public sealed record ArmoryStat(string Type, string Name, int Value, IReadOnlyList<string> Effects);

/// <summary>Daevanion-Brett: wie viele Knoten offen sind.</summary>
public sealed record ArmoryBoard(int Id, string Name, string? Icon, int Open, int Total, int Percent);

/// <summary>Titel-Fortschritt einer Kategorie (Angriff/Verteidigung/Sonstiges) mit dem angelegten Titel.</summary>
public sealed record ArmoryTitle(string Category, string Name, string Grade, int Owned, int Total);

/// <summary>Pet oder Flügel (Name, Stufe bzw. Verzauberung, Icon).</summary>
public sealed record ArmoryExtra(string Name, string Grade, int Level, string? Icon);

public sealed record ArmoryProfile(
    string Name, string Server, int ServerId, string ClassName, int Level, int ItemLevel, int CombatPower, string Race,
    IReadOnlyList<ArmoryItem> Equipment, IReadOnlyList<ArmorySkill> Skills, DateTime LoadedAt)
{
    public string CharacterId { get; init; } = "";
    public int ClassId { get; init; }
    public string? Portrait { get; init; }
    public IReadOnlyList<ArmoryStat> Stats { get; init; } = [];
    public IReadOnlyList<ArmoryBoard> Boards { get; init; } = [];
    public IReadOnlyList<ArmoryTitle> Titles { get; init; } = [];
    public int TitlesOwned { get; init; }
    public ArmoryExtra? Pet { get; init; }
    public ArmoryExtra? Wing { get; init; }

    /// <summary>Mittlere Verzauberung über Waffe und Rüstung (ohne Schmuck und Runen).</summary>
    public double AverageEnchant
    {
        get
        {
            var gear = Equipment.Where(i => Armory.IsArmorSlot(i.Slot)).ToList();
            return gear.Count == 0 ? 0 : gear.Average(i => i.Enchant + i.Exceed);
        }
    }

    /// <summary>Anteil offener Daevanion-Knoten über alle Bretter.</summary>
    public int DaevanionPercent
    {
        get
        {
            int total = Boards.Sum(b => b.Total);
            return total == 0 ? 0 : (int)Math.Round(Boards.Sum(b => b.Open) * 100.0 / total);
        }
    }
}

/// <summary>Ein Wert eines Items (Hauptwert, Zusatzwert, Manastein).</summary>
public sealed record ArmoryItemStat(string Name, string Value, string Grade = "");

/// <summary>Details eines angelegten Items: Werte, Manasteine, Götterstein.</summary>
public sealed record ArmoryItemDetail(
    string Name, string Grade, string Category, int Level, int Enchant, int MaxEnchant,
    IReadOnlyList<ArmoryItemStat> MainStats, IReadOnlyList<ArmoryItemStat> SubStats,
    IReadOnlyList<ArmoryItemStat> MagicStones, int MagicStoneSlots,
    IReadOnlyList<(string Name, string Description)> GodStones, bool Tradable);

/// <summary>
/// Öffentliche Charakterdaten von NCSOFT (dieselben wie auf aion2.plaync.com): Stufe, Itemstufe,
/// Kampfkraft, Ausrüstung, Skills, Werte, Daevanion, Titel. Abfrage nur auf Wunsch des Nutzers
/// (Klick, Profil-Reiter, Gruppenbeitritt), mit Zwischenspeicher und Pause zwischen Anfragen.
/// Endpunkte wie in nuriland/aion2-api (MIT). NCSOFT aktualisiert die Daten zeitversetzt
/// (oft erst nach dem Ausloggen).
/// </summary>
public sealed partial class Armory
{
    private const string SearchUrl = "https://api-search.plaync.com/aion2global/search/v2/character";
    private const string SiteUrl = "https://aion2.plaync.com/api/character";
    /// <summary>So lange gilt ein geladenes Profil (Speicher und Ordner <c>Profile</c>), damit Gruppe und Profil-Reiter
    /// nicht bei jedem Wiedersehen neu fragen. Der eigene Verlauf und „Aktualisieren“ holen frisch (<c>fresh</c>).</summary>
    public static readonly TimeSpan CacheTime = TimeSpan.FromHours(6);

    /// <summary>Nur Bilder von diesen NCSOFT-Servern werden geladen.</summary>
    public static bool IsOfficialImage(string? url) =>
        url is not null && (url.StartsWith("https://assets.playnccdn.com/", StringComparison.Ordinal)
                            || url.StartsWith("https://profileimg.plaync.com/", StringComparison.Ordinal));

    private static readonly HashSet<string> ArmorSlots =
        ["MainHand", "SubHand", "Torso", "Pants", "Helmet", "Shoulder", "Gloves", "Boots", "Cape"];

    public static bool IsArmorSlot(string slot) => ArmorSlots.Contains(slot);

    private readonly HttpClient http;
    private readonly SemaphoreSlim gate = new(1, 1);              // Profile: Suche → Info → Ausrüstung nacheinander
    private readonly SemaphoreSlim itemGate = new(3, 3);          // Item-Details: bis zu 3 gleichzeitig unterwegs …
    private readonly SemaphoreSlim backgroundGate = new(2, 2);    // … davon höchstens 2 fürs Vorladen, einer bleibt für Klicks frei
    private readonly Lock rate = new();
    private readonly Dictionary<(int, string), ArmoryProfile> cache = [];
    private readonly Dictionary<(string, long, int), ArmoryItemDetail> itemCache = [];
    private DateTime lastRequest;

    public Armory(HttpMessageHandler? handler = null)
    {
        http = handler is null ? new HttpClient() : new HttpClient(handler);
        http.Timeout = TimeSpan.FromSeconds(20);
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("AetherDPS", "1.0"));
    }

    /// <summary>EU-Region; Sprache der Oberfläche, sofern NCSOFT sie anbietet (sonst Englisch).</summary>
    private static string Lang => Loc.Language switch
    {
        "de" => "de-DE", "fr" => "fr-FR", "es" => "es-ES", "ja" => "ja-JP", "pt" => "pt-BR", _ => "en-US",
    };

    /// <summary>„#1317“ oder „1317“ → 1317.</summary>
    /// <summary>Ordner für Item-Details auf der Platte (Datenordner\Profile); null = nur im Speicher.</summary>
    public string? CacheDir { get; set; }

    /// <summary>So lange gelten Item-Details aus dem Ordner (Manasteine/Götterstein können sich ändern).</summary>
    public static readonly TimeSpan ItemCacheTime = TimeSpan.FromHours(6);

    public static int? ServerId(string? server) =>
        server is not null && int.TryParse(server.TrimStart('#'), out var id) && id > 1000 ? id : null;

    /// <summary>Schon geladenes Profil (ohne Anfrage): aus dem Speicher, sonst aus dem Ordner, höchstens <see cref="CacheTime"/> alt.</summary>
    public ArmoryProfile? Cached(string name, int serverId)
    {
        lock (cache)
            if (cache.TryGetValue((serverId, name), out var hit) && DateTime.Now - hit.LoadedAt < CacheTime) return hit;
        if (ReadProfile(name, serverId) is { } stored)
        {
            lock (cache) cache[(serverId, name)] = stored;
            return stored;
        }
        return null;
    }

    /// <summary>
    /// Profil per Name und Server laden (null = nicht gefunden oder keine Verbindung). Bis <see cref="CacheTime"/> aus
    /// Speicher/Ordner; <paramref name="fresh"/> fragt trotzdem NCSOFT (eigener Verlauf, „Aktualisieren“).
    /// </summary>
    public async Task<ArmoryProfile?> LoadAsync(string name, int serverId, CancellationToken ct = default, bool fresh = false)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        name = name.Trim();
        if (!fresh && Cached(name, serverId) is { } hit) return hit;

        await gate.WaitAsync(ct);
        try
        {
            if (!fresh && Cached(name, serverId) is { } again) return again;   // kam während des Wartens
            var id = await FindAsync(name, serverId, ct);
            if (id is null) return null;
            var query = $"lang={Lang}&region=eu&serverId={serverId}&characterId={id}";
            // Info und Ausrüstung gleichzeitig (gestaffelt gestartet), spart eine Antwortzeit
            var infoTask = GetAsync($"{SiteUrl}/info?{query}", ct);
            var equipmentTask = GetAsync($"{SiteUrl}/equipment?{query}", ct);
            using var info = await infoTask;
            using var equipment = await equipmentTask;
            if (info is null) return null;
            var profile = Parse(info.RootElement, equipment?.RootElement) with { CharacterId = id };
            lock (cache) cache[(serverId, name)] = profile;
            WriteProfile(name, serverId, profile);
            return profile;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return null;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Details eines Items aus dem Profil (Werte, Manasteine, Götterstein). Aus dem Speicher, sonst aus dem Ordner
    /// (<see cref="ItemCacheTime"/>), sonst von NCSOFT. Bis zu drei Anfragen laufen gleichzeitig, gestartet wird
    /// höchstens alle 0,5 s; <paramref name="background"/> (Vorladen) nutzt nur zwei der drei Plätze.
    /// </summary>
    public async Task<ArmoryItemDetail?> LoadItemAsync(ArmoryProfile profile, ArmoryItem item, CancellationToken ct = default, bool background = false)
    {
        if (item.Id == 0 || string.IsNullOrEmpty(profile.CharacterId)) return null;
        var key = (profile.CharacterId, item.Id, item.SlotPos);
        lock (itemCache)
            if (itemCache.TryGetValue(key, out var hit)) return hit;
        var file = CacheFile(profile, item);
        if (ReadCached(file) is { } stored)
        {
            lock (itemCache) itemCache[key] = stored;
            return stored;
        }

        if (background) await backgroundGate.WaitAsync(ct);
        try
        {
            await itemGate.WaitAsync(ct);
            try
            {
                lock (itemCache)
                    if (itemCache.TryGetValue(key, out var again)) return again;   // kam während des Wartens
                var query = $"id={item.Id}&enchantLevel={item.Enchant}&slotPos={item.SlotPos}"
                            + $"&lang={Lang}&region=eu&serverId={profile.ServerId}&characterId={profile.CharacterId}";
                using var doc = await GetAsync($"{SiteUrl}/equipment/item?{query}", ct);
                if (doc is null) return null;
                var detail = ParseItem(doc.RootElement);
                lock (itemCache) itemCache[key] = detail;
                WriteCached(file, doc.RootElement.GetRawText());
                return detail;
            }
            finally
            {
                itemGate.Release();
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return null;
        }
        finally
        {
            if (background) backgroundGate.Release();
        }
    }

    private string? ProfileFile(string name, int serverId)
    {
        if (CacheDir is null) return null;
        var safe = string.Concat($"{serverId}_{name}_{Lang}".Select(c => char.IsLetterOrDigit(c) || c is '_' or '-' ? c : '_'));
        return Path.Combine(CacheDir, "profil_" + safe + ".json");
    }

    private ArmoryProfile? ReadProfile(string name, int serverId)
    {
        try
        {
            var file = ProfileFile(name, serverId);
            if (file is null || !File.Exists(file)) return null;
            var profile = JsonSerializer.Deserialize<ArmoryProfile>(File.ReadAllText(file));
            return profile is not null && DateTime.Now - profile.LoadedAt < CacheTime ? profile : null;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException or NotSupportedException) { return null; }
    }

    private void WriteProfile(string name, int serverId, ArmoryProfile profile)
    {
        try
        {
            if (ProfileFile(name, serverId) is not { } file) return;
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, JsonSerializer.Serialize(profile));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException) { }
    }

    private string? CacheFile(ArmoryProfile profile, ArmoryItem item)
    {
        if (CacheDir is null) return null;
        var name = string.Concat($"{profile.ServerId}_{profile.CharacterId}_{item.Id}_{item.Enchant}_{item.SlotPos}_{Lang}"
            .Select(c => char.IsLetterOrDigit(c) || c is '_' or '-' ? c : '_'));
        return Path.Combine(CacheDir, name + ".json");
    }

    private static ArmoryItemDetail? ReadCached(string? file)
    {
        try
        {
            if (file is null || !File.Exists(file) || DateTime.Now - File.GetLastWriteTime(file) > ItemCacheTime) return null;
            using var doc = JsonDocument.Parse(File.ReadAllText(file));
            return ParseItem(doc.RootElement);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException or InvalidOperationException) { return null; }
    }

    private static void WriteCached(string? file, string json)
    {
        try
        {
            if (file is null) return;
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, json);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    private async Task<string?> FindAsync(string name, int serverId, CancellationToken ct)
    {
        using var doc = await GetAsync($"{SearchUrl}?keyword={Uri.EscapeDataString(name)}&region=eu&localeInfo={Lang}&serverId={serverId}", ct);
        if (doc is null || !doc.RootElement.TryGetProperty("list", out var list)) return null;
        foreach (var row in list.EnumerateArray())
        {
            var found = Tags().Replace(row.GetProperty("name").GetString() ?? "", "");
            if (string.Equals(found, name, StringComparison.OrdinalIgnoreCase)
                && row.TryGetProperty("serverId", out var s) && s.GetInt32() == serverId)
                return row.GetProperty("characterId").GetString();   // schon URL-kodiert
        }
        return null;
    }

    private async Task<JsonDocument?> GetAsync(string url, CancellationToken ct)
    {
        // höflich bleiben: höchstens 2 Anfragen pro Sekunde starten (auch wenn mehrere gleichzeitig laufen)
        TimeSpan wait;
        lock (rate)
        {
            var start = lastRequest.AddMilliseconds(500) is var next && next > DateTime.Now ? next : DateTime.Now;
            lastRequest = start;
            wait = start - DateTime.Now;
        }
        if (wait > TimeSpan.Zero) await Task.Delay(wait, ct);
        using var response = await http.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode) return null;
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
    }

    public static ArmoryProfile Parse(JsonElement info, JsonElement? equipment)
    {
        var p = info.GetProperty("profile");
        int itemLevel = 0;
        var stats = new List<ArmoryStat>();
        if (info.TryGetProperty("stat", out var stat) && stat.TryGetProperty("statList", out var statList) && statList.ValueKind == JsonValueKind.Array)
            foreach (var s in statList.EnumerateArray())
            {
                var type = Str(s, "type");
                if (type == "ItemLevel")
                {
                    itemLevel = Int(s, "value");
                    continue;
                }
                stats.Add(new ArmoryStat(type, Str(s, "name"), Int(s, "value"), Strings(s, "statSecondList")));
            }

        var boards = new List<ArmoryBoard>();
        if (info.TryGetProperty("daevanion", out var dv) && dv.TryGetProperty("boardList", out var bl) && bl.ValueKind == JsonValueKind.Array)
            foreach (var b in bl.EnumerateArray())
                boards.Add(new ArmoryBoard(Int(b, "id"), Str(b, "name"), Opt(b, "icon"), Int(b, "openNodeCount"), Int(b, "totalNodeCount"), Int(b, "openPercent")));

        var titles = new List<ArmoryTitle>();
        int titlesOwned = 0;
        if (info.TryGetProperty("title", out var tt) && tt.ValueKind == JsonValueKind.Object)
        {
            titlesOwned = Int(tt, "ownedCount");
            if (tt.TryGetProperty("titleList", out var tl) && tl.ValueKind == JsonValueKind.Array)
                foreach (var t in tl.EnumerateArray())
                    titles.Add(new ArmoryTitle(Str(t, "equipCategory"), Str(t, "name"), Str(t, "grade"), Int(t, "ownedCount"), Int(t, "totalCount")));
        }

        var items = new List<ArmoryItem>();
        var skills = new List<ArmorySkill>();
        ArmoryExtra? pet = null, wing = null;
        if (equipment is { } eq)
        {
            if (eq.TryGetProperty("equipment", out var e) && e.TryGetProperty("equipmentList", out var list) && list.ValueKind == JsonValueKind.Array)
                foreach (var i in list.EnumerateArray())
                    items.Add(new ArmoryItem(Str(i, "slotPosName"), Str(i, "name"), Str(i, "grade"), Int(i, "enchantLevel"), Opt(i, "icon"),
                        Long(i, "id"), Int(i, "slotPos"), Int(i, "exceedLevel")));
            if (eq.TryGetProperty("skill", out var sk) && sk.TryGetProperty("skillList", out var sl) && sl.ValueKind == JsonValueKind.Array)
                foreach (var s in sl.EnumerateArray())
                    if (Int(s, "skillLevel") > 0)
                        skills.Add(new ArmorySkill(Str(s, "name"), Str(s, "category"), Int(s, "skillLevel"), Opt(s, "icon")));
            if (eq.TryGetProperty("petwing", out var pw) && pw.ValueKind == JsonValueKind.Object)
            {
                if (pw.TryGetProperty("pet", out var pe) && pe.ValueKind == JsonValueKind.Object && Str(pe, "name") != "")
                    pet = new ArmoryExtra(Str(pe, "name"), "", Int(pe, "level"), Opt(pe, "icon"));
                if (pw.TryGetProperty("wing", out var w) && w.ValueKind == JsonValueKind.Object && Str(w, "name") != "")
                    wing = new ArmoryExtra(Str(w, "name"), Str(w, "grade"), Int(w, "enchantLevel"), Opt(w, "icon"));
            }
        }
        return new ArmoryProfile(Str(p, "characterName"), Str(p, "serverName"), Int(p, "serverId"), Str(p, "className"),
            Int(p, "characterLevel"), itemLevel, Int(p, "combatPower"), Str(p, "raceName"), items,
            skills.OrderByDescending(s => s.Level).ToList(), DateTime.Now)
        {
            CharacterId = Str(p, "characterId"),
            ClassId = Int(p, "pcId"),
            Portrait = Opt(p, "profileImage"),
            Stats = stats,
            Boards = boards,
            Titles = titles,
            TitlesOwned = titlesOwned,
            Pet = pet,
            Wing = wing,
        };
    }

    public static ArmoryItemDetail ParseItem(JsonElement d)
    {
        static List<ArmoryItemStat> Stats(JsonElement d, string name, bool withExtra)
        {
            var list = new List<ArmoryItemStat>();
            if (!d.TryGetProperty(name, out var arr) || arr.ValueKind != JsonValueKind.Array) return list;
            foreach (var s in arr.EnumerateArray())
            {
                var value = Str(s, "value");
                var extra = Str(s, "extra");
                if (withExtra && extra is not ("" or "0")) value += $" (+{extra})";
                list.Add(new ArmoryItemStat(Str(s, "name"), value, Str(s, "grade")));
            }
            return list;
        }

        var gods = new List<(string, string)>();
        if (d.TryGetProperty("godStoneStat", out var g) && g.ValueKind == JsonValueKind.Array)
            foreach (var s in g.EnumerateArray())
                gods.Add((Str(s, "name"), Str(s, "desc")));
        return new ArmoryItemDetail(Str(d, "name"), Str(d, "grade"), Str(d, "categoryName"), Int(d, "level"), Int(d, "enchantLevel"),
            Int(d, "maxEnchantLevel"), Stats(d, "mainStats", true), Stats(d, "subStats", false), Stats(d, "magicStoneStat", false),
            Int(d, "magicStoneSlotCount"), gods, d.TryGetProperty("tradable", out var t) && t.ValueKind == JsonValueKind.True);
    }

    private static string Str(JsonElement e, string name) => e.TryGetProperty(name, out var v) ? v.ValueKind switch
    {
        JsonValueKind.String => v.GetString() ?? "",
        JsonValueKind.Number => v.GetRawText(),
        _ => "",
    } : "";

    private static string? Opt(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    private static int Int(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i) ? i : 0;
    private static long Long(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var i) ? i : 0;

    private static List<string> Strings(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Array
            ? v.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString() ?? "").ToList()
            : [];

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex Tags();
}
