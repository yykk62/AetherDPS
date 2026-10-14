using System.Diagnostics;

namespace AetherDPS.App.Services;

public static class Browser
{
    /// <summary>Nur https-Links öffnen (Discord-Link kommt aus status.json).</summary>
    public static void Open(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps) return;
        try
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Link konnte nicht geöffnet werden");
        }
    }
}
