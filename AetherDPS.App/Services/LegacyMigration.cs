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

    /// <summary>Entpackte Dateien der alten Version (%TEMP%\.net\Aion2Meter) wegräumen.</summary>
    public static void CleanupOldExtraction()
    {
        try
        {
            var dir = Path.Combine(Path.GetTempPath(), ".net", OldName);
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
        catch (Exception)
        {
            // nur Aufräumen
        }
    }
}
