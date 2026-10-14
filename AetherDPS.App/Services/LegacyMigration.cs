using Serilog;

namespace AetherDPS.App.Services;

/// <summary>
/// Bis Version 0.5.0 hieß der Meter „Aion2Meter“. Beim ersten Start als AetherDPS ziehen
/// Einstellungen, Läufe und Timer in den neuen Datenordner um.
/// </summary>
public static class LegacyMigration
{
    private const string OldName = "Aion2Meter";

    /// <summary>Alten Datenordner übernehmen, falls es den neuen noch nicht gibt. Gibt den zu nutzenden Ordner zurück.</summary>
    public static string MoveDataDir(string newDir)
    {
        var oldDir = Path.Combine(Path.GetDirectoryName(newDir)!, OldName);
        if (Directory.Exists(newDir) || !Directory.Exists(oldDir)) return newDir;
        try
        {
            Directory.Move(oldDir, newDir);
        }
        catch (Exception)
        {
            // Datei noch offen o. Ä.: dann eben im alten Ordner weiterarbeiten, nächster Start versucht es erneut
            return oldDir;
        }
        RenameFile(newDir, "aion2meter.json", "aetherdps.json");
        return newDir;
    }

    private static void RenameFile(string dir, string oldName, string newName)
    {
        var from = Path.Combine(dir, oldName);
        var to = Path.Combine(dir, newName);
        try
        {
            if (File.Exists(from) && !File.Exists(to)) File.Move(from, to);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Umbenennen von {File} fehlgeschlagen", oldName);
        }
    }

    /// <summary>
    /// Entpackte Dateien älterer Versionen wegräumen. Die exe entpackt sich nach
    /// %TEMP%\.net\&lt;Name der exe&gt;\&lt;hash&gt; – der Name hängt an der Datei: Nach dem Update aus 0.5.0
    /// heißt sie weiter Aion2Meter.exe. Der eigene Ordner wird deshalb NIE gelöscht, egal unter welchem
    /// Namen (0.6.0 löschte ihn beim Start und stürzte ab, Bericht #1 vom 14.10.2026).
    /// </summary>
    public static void CleanupExtractions(string baseDir)
    {
        try
        {
            var current = new DirectoryInfo(baseDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            var net = current.Parent?.Parent;
            if (net is null || net.Name != ".net") return;   // nur die veröffentlichte Einzeldatei
            foreach (var app in new[] { "AetherDPS", OldName })
            {
                var dir = new DirectoryInfo(Path.Combine(net.FullName, app));
                if (!dir.Exists) continue;
                foreach (var version in dir.GetDirectories())
                {
                    if (version.FullName.Equals(current.FullName, StringComparison.OrdinalIgnoreCase)) continue;
                    try { version.Delete(recursive: true); } catch (Exception) { /* wird noch benutzt */ }
                }
            }
        }
        catch (Exception)
        {
            // nur Aufräumen
        }
    }
}
