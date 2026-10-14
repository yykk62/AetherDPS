using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AetherDPS.Engine.Community;

/// <summary>Eine Nachricht an alle (status.json im Repo), z. B. „Update kommt in einer Stunde“.</summary>
public sealed record Announcement(string Id, string Level, string Text, DateTime? Until);

/// <summary>Inhalt von status.json: Nachrichten, Discord-Link und die Adresse des Servers.</summary>
public sealed record CommunityStatus(IReadOnlyList<Announcement> Messages, string? Discord, string? Api);

/// <summary>
/// Alles, was AetherDPS ins Netz schickt – und das ist wenig:
/// <list type="bullet">
/// <item>Ein Zähler-Signal pro Tag, nur mit Zustimmung: Version, Sprache, „neu installiert ja/nein“.
/// Keine Id, kein Name, kein Server.</item>
/// <item>Fehlerberichte, nur wenn du auf „Senden“ klickst (Namen werden vorher entfernt).</item>
/// <item>Lesen von status.json auf GitHub (Nachrichten an alle, Discord-Link) – sendet nichts.</item>
/// </list>
/// Der Server dazu liegt offen in <c>server/</c>.
/// </summary>
public sealed class CommunityService
{
    public const string StatusUrl = "https://raw.githubusercontent.com/yykk62/AetherDPS/main/status.json";
    public const string DefaultApi = "https://aetherdps-api.pixel6269.workers.dev";

    private readonly HttpClient http;
    private readonly MeterSettings settings;
    private readonly string version;
    private readonly string statusUrl;
    private string api;

    public CommunityService(MeterSettings settings, Version version, HttpMessageHandler? handler = null)
    {
        this.settings = settings;
        this.version = $"{version.Major}.{version.Minor}.{Math.Max(0, version.Build)}";
        http = handler is null ? new HttpClient() : new HttpClient(handler);
        http.Timeout = TimeSpan.FromSeconds(20);
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("AetherDPS", this.version));
        // Für Tests gegen einen lokalen Server
        api = (Environment.GetEnvironmentVariable("AETHERDPS_API") ?? DefaultApi).TrimEnd('/');
        statusUrl = Environment.GetEnvironmentVariable("AETHERDPS_STATUS") ?? StatusUrl;
    }

    public CommunityStatus? Status { get; private set; }

    /// <summary>Das, was beim Zähler gesendet würde – zum Anzeigen vor der Zustimmung.</summary>
    public string PingExample() => JsonSerializer.Serialize(PingBody(isNew: settings.LastPingDay is null));

    private object PingBody(bool isNew) => new { v = version, lang = Loc.Language, @new = isNew };

    /// <summary>Höchstens ein Signal pro UTC-Tag, nur mit Zustimmung. true = gesendet.</summary>
    public async Task<bool> PingIfDueAsync(CancellationToken ct = default)
    {
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        if (settings.UsageStats != true || settings.LastPingDay == today) return false;
        try
        {
            using var response = await http.PostAsJsonAsync($"{api}/v1/ping", PingBody(isNew: settings.LastPingDay is null), ct);
            if (!response.IsSuccessStatusCode) return false;
            settings.LastPingDay = today;
            settings.Save();
            return true;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return false;   // offline – morgen wieder
        }
    }

    /// <summary>Fehlerbericht senden. Gibt die Nummer der Meldung zurück, null bei Fehler.</summary>
    public async Task<int?> SendReportAsync(string kind, string message, string? log, CancellationToken ct = default)
    {
        var body = new
        {
            kind,
            v = version,
            lang = Loc.Language,
            os = Environment.OSVersion.VersionString,
            message = Cut(message, 4000),
            log = log is null ? null : CutStart(log, 190_000),
        };
        try
        {
            using var response = await http.PostAsJsonAsync($"{api}/v1/report", body, ct);
            if (!response.IsSuccessStatusCode) return null;
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            return json.RootElement.TryGetProperty("id", out var id) ? id.GetInt32() : 0;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return null;
        }
    }

    /// <summary>status.json von GitHub lesen. Nachrichten in der Sprache (sonst Englisch), nur gültige für diese Version.</summary>
    public async Task<CommunityStatus?> RefreshStatusAsync(CancellationToken ct = default)
    {
        try
        {
            var file = await http.GetFromJsonAsync<StatusFile>(statusUrl, ct);
            if (file is null) return Status;
            var current = Version.Parse(version);
            var messages = new List<Announcement>();
            foreach (var m in file.Messages ?? [])
            {
                if (string.IsNullOrWhiteSpace(m.Id) || m.Text is null) continue;
                if (m.Until is DateTime until && until < DateTime.UtcNow) continue;
                if (m.MinVersion is { } min && Version.TryParse(min, out var minV) && current < minV) continue;
                if (m.MaxVersion is { } max && Version.TryParse(max, out var maxV) && current > maxV) continue;
                var text = m.Text.GetValueOrDefault(Loc.Language) ?? m.Text.GetValueOrDefault("en") ?? m.Text.Values.FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(text)) messages.Add(new Announcement(m.Id, m.Level ?? "info", text, m.Until));
            }
            if (file.Api is { Length: > 0 } newApi && Uri.TryCreate(newApi, UriKind.Absolute, out var uri) && uri.Scheme == "https")
                api = newApi.TrimEnd('/');
            Status = new CommunityStatus(messages, string.IsNullOrWhiteSpace(file.Discord) ? null : file.Discord, file.Api);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or NotSupportedException)
        {
            // offline oder Datei kaputt – alte Infos behalten
        }
        return Status;
    }

    private static string Cut(string s, int max) => s.Length <= max ? s : s[..max];

    /// <summary>Bei Logs zählt das Ende (die neuesten Zeilen).</summary>
    private static string CutStart(string s, int max) => s.Length <= max ? s : "…\n" + s[^max..];

    private sealed class StatusFile
    {
        [JsonPropertyName("messages")] public List<StatusMessage>? Messages { get; set; }
        [JsonPropertyName("discord")] public string? Discord { get; set; }
        [JsonPropertyName("api")] public string? Api { get; set; }
    }

    private sealed class StatusMessage
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("level")] public string? Level { get; set; }
        [JsonPropertyName("text")] public Dictionary<string, string>? Text { get; set; }
        [JsonPropertyName("until")] public DateTime? Until { get; set; }
        [JsonPropertyName("minVersion")] public string? MinVersion { get; set; }
        [JsonPropertyName("maxVersion")] public string? MaxVersion { get; set; }
    }
}
