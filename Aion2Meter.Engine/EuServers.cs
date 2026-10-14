namespace Aion2Meter.Engine;

/// <summary>
/// Namen der EU-Server. RATmeter kennt nur Korea/Taiwan und liefert für EU „#&lt;Nummer&gt;“
/// (siehe Patch in ServerMap). Bekannte Nummern hier eintragen.
/// </summary>
public static class EuServers
{
    private static readonly Dictionary<int, string> Names = new()
    {
        [1317] = "Hogalum",   // geführte Aufnahme 14.10.2026
    };

    /// <summary>„#1317“ → „Hogalum“, Unbekanntes bleibt wie es ist.</summary>
    public static string Display(string? server)
    {
        if (string.IsNullOrWhiteSpace(server)) return "";
        return server.StartsWith('#') && int.TryParse(server.AsSpan(1), out var id) && Names.TryGetValue(id, out var name) ? name : server;
    }
}
