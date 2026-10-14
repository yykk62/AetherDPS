using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AetherDPS.Engine.Community;

public sealed record ArmoryItem(string Slot, string Name, string Grade, int Enchant, string? Icon);

public sealed record ArmorySkill(string Name, string Category, int Level, string? Icon);

public sealed record ArmoryProfile(
    string Name, string Server, int ServerId, string ClassName, int Level, int ItemLevel, int CombatPower, string Race,
    IReadOnlyList<ArmoryItem> Equipment, IReadOnlyList<ArmorySkill> Skills, DateTime LoadedAt);

/// <summary>
/// Öffentliche Charakterdaten von NCSOFT (dieselben wie auf aion2.plaync.com): Stufe, Itemstufe,
/// Kampfkraft, Ausrüstung, Skills. Wird nur auf Klick abgefragt, mit Zwischenspeicher und Pause
/// zwischen Anfragen. Endpunkte wie in nuriland/aion2-api (MIT). NCSOFT aktualisiert die Daten
/// zeitversetzt (oft erst nach dem Ausloggen).
/// </summary>
public sealed partial class Armory
{
    private const string SearchUrl = "https://api-search.plaync.com/aion2global/search/v2/character";
    private const string SiteUrl = "https://aion2.plaync.com/api/character";
    private static readonly TimeSpan CacheTime = TimeSpan.FromMinutes(15);

    private readonly HttpClient http;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Dictionary<(int, string), ArmoryProfile> cache = [];
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
    public static int? ServerId(string? server) =>
        server is not null && int.TryParse(server.TrimStart('#'), out var id) && id > 1000 ? id : null;

    /// <summary>Profil per Name und Server laden (null = nicht gefunden oder keine Verbindung).</summary>
    public async Task<ArmoryProfile?> LoadAsync(string name, int serverId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        lock (cache)
            if (cache.TryGetValue((serverId, name), out var hit) && DateTime.Now - hit.LoadedAt < CacheTime) return hit;

        await gate.WaitAsync(ct);
        try
        {
            var id = await FindAsync(name, serverId, ct);
            if (id is null) return null;
            var query = $"lang={Lang}&region=eu&serverId={serverId}&characterId={id}";
            using var info = await GetAsync($"{SiteUrl}/info?{query}", ct);
            using var equipment = await GetAsync($"{SiteUrl}/equipment?{query}", ct);
            if (info is null) return null;
            var profile = Parse(info.RootElement, equipment?.RootElement);
            lock (cache) cache[(serverId, name)] = profile;
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
        // höflich bleiben: höchstens ~2 Anfragen pro Sekunde
        var wait = lastRequest.AddMilliseconds(500) - DateTime.Now;
        if (wait > TimeSpan.Zero) await Task.Delay(wait, ct);
        lastRequest = DateTime.Now;
        using var response = await http.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode) return null;
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
    }

    public static ArmoryProfile Parse(JsonElement info, JsonElement? equipment)
    {
        var p = info.GetProperty("profile");
        int itemLevel = 0;
        if (info.TryGetProperty("stat", out var stat) && stat.TryGetProperty("statList", out var stats))
            foreach (var s in stats.EnumerateArray())
                if (s.TryGetProperty("type", out var t) && t.GetString() == "ItemLevel") itemLevel = s.GetProperty("value").GetInt32();

        var items = new List<ArmoryItem>();
        var skills = new List<ArmorySkill>();
        if (equipment is { } eq)
        {
            if (eq.TryGetProperty("equipment", out var e) && e.TryGetProperty("equipmentList", out var list))
                foreach (var i in list.EnumerateArray())
                    items.Add(new ArmoryItem(Str(i, "slotPosName"), Str(i, "name"), Str(i, "grade"), Int(i, "enchantLevel"), Opt(i, "icon")));
            if (eq.TryGetProperty("skill", out var sk) && sk.TryGetProperty("skillList", out var sl))
                foreach (var s in sl.EnumerateArray())
                    if (Int(s, "skillLevel") > 0)
                        skills.Add(new ArmorySkill(Str(s, "name"), Str(s, "category"), Int(s, "skillLevel"), Opt(s, "icon")));
        }
        return new ArmoryProfile(Str(p, "characterName"), Str(p, "serverName"), Int(p, "serverId"), Str(p, "className"),
            Int(p, "characterLevel"), itemLevel, Int(p, "combatPower"), Str(p, "raceName"), items,
            skills.OrderByDescending(s => s.Level).ToList(), DateTime.Now);
    }

    private static string Str(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
    private static string? Opt(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    private static int Int(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : 0;

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex Tags();
}
