namespace AetherDPS.Engine;

/// <summary>
/// Servernamen. RATmeter kennt nur Korea/Taiwan und liefert für alle anderen „#&lt;Nummer&gt;“
/// (siehe Patch in ServerMap). Die Namen kommen in der gewählten Sprache aus
/// GameData/servers.json (EU = 13xx Elyos, 23xx Asmodier; z. B. 1317 = Hogalum / Tsenka).
/// </summary>
public static class EuServers
{
    /// <summary>„#1317“ → „Hogalum“, Unbekanntes bleibt wie es ist.</summary>
    public static string Display(string? server)
    {
        if (string.IsNullOrWhiteSpace(server)) return "";
        return server.StartsWith('#') && int.TryParse(server.AsSpan(1), out var id) && GameText.ServerName(id) is { } name ? name : server;
    }
}
