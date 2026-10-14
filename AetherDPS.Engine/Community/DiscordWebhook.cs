using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AetherDPS.Engine.Community;

/// <summary>
/// Läufe in einen Discord-Kanal posten – über einen Webhook, den du selbst in Discord anlegst
/// (Kanal → Bearbeiten → Integrationen → Webhooks) und in den Einstellungen einträgst.
/// Gesendet wird nur auf Klick, und nur an diese Adresse. Idee aus Accapura/AionDpsMeter (GPL-3.0).
/// </summary>
public static partial class DiscordWebhook
{
    [GeneratedRegex(@"^https://(?:ptb\.|canary\.)?discord(?:app)?\.com/api/webhooks/\d+/[\w-]+$")]
    private static partial Regex Pattern();

    public static bool IsValid(string? url) => !string.IsNullOrWhiteSpace(url) && Pattern().IsMatch(url.Trim());

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    /// <summary>Text (Markdown, max. 2000 Zeichen) und optional ein PNG senden. true = angekommen.</summary>
    public static async Task<bool> SendAsync(string url, string text, byte[]? png = null, CancellationToken ct = default)
    {
        if (!IsValid(url)) return false;
        using var form = new MultipartFormDataContent();
        var payload = JsonSerializer.Serialize(new
        {
            content = text.Length <= 2000 ? text : text[..1997] + "…",
            username = "AetherDPS",
            allowed_mentions = new { parse = Array.Empty<string>() },   // nie @everyone o. Ä. auslösen
        });
        form.Add(new StringContent(payload, System.Text.Encoding.UTF8, "application/json"), "payload_json");
        if (png is not null)
        {
            var file = new ByteArrayContent(png);
            file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            form.Add(file, "files[0]", "aetherdps.png");
        }
        try
        {
            using var response = await Http.PostAsync(url.Trim(), form, ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return false;
        }
    }
}
