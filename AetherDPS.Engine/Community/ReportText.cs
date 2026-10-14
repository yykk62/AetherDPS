using System.Text.RegularExpressions;

namespace AetherDPS.Engine.Community;

/// <summary>Log und Absturz für Fehlerberichte vorbereiten – ohne Namen von Spielern.</summary>
public static partial class ReportText
{
    private const string CrashFileName = "absturz.txt";

    /// <summary>Unbehandelten Fehler für den nächsten Start merken („Fehlerbericht senden?“).</summary>
    public static void WriteCrash(string dataDir, Exception ex)
    {
        try
        {
            File.WriteAllText(Path.Combine(dataDir, CrashFileName), $"{DateTime.Now:O}\n{ex}");
        }
        catch (Exception)
        {
            // beim Absturz nicht noch einen Fehler werfen
        }
    }

    /// <summary>Gemerkten Absturz lesen und die Datei löschen (wird nur einmal angeboten).</summary>
    public static string? TakeCrash(string dataDir)
    {
        var file = Path.Combine(dataDir, CrashFileName);
        try
        {
            if (!File.Exists(file)) return null;
            var text = File.ReadAllText(file);
            File.Delete(file);
            return text;
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>Die neuesten Zeilen der aktuellen Log-Datei (Serilog teilt pro Tag).</summary>
    public static string? ReadLatestLog(string dataDir, int maxChars = 150_000)
    {
        var dir = Path.Combine(dataDir, "logs");
        if (!Directory.Exists(dir)) return null;
        var latest = new DirectoryInfo(dir).GetFiles("*.log").OrderByDescending(f => f.LastWriteTimeUtc).FirstOrDefault();
        if (latest is null) return null;
        try
        {
            // Die Datei ist von Serilog geöffnet – mit FileShare.ReadWrite lesen
            using var stream = new FileStream(latest.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (stream.Length > maxChars * 2) stream.Seek(-maxChars * 2, SeekOrigin.End);
            using var reader = new StreamReader(stream);
            var text = reader.ReadToEnd();
            return text.Length <= maxChars ? text : text[^maxChars..];
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>
    /// Bekannte Namen (eigener Charakter, Gruppe, Spieler der Sitzung) durch „Spieler 1“, „Spieler 2“ …
    /// ersetzen, außerdem den Windows-Benutzernamen in Pfaden.
    /// </summary>
    public static string Scrub(string text, IEnumerable<string?> names)
    {
        int n = 0;
        foreach (var name in names.Where(x => !string.IsNullOrWhiteSpace(x) && x!.Length >= 2 && !x.StartsWith("Player_", StringComparison.Ordinal))
                     .Select(x => x!).Distinct().OrderByDescending(x => x.Length))
        {
            if (!text.Contains(name, StringComparison.OrdinalIgnoreCase)) continue;
            text = Regex.Replace(text, Regex.Escape(name), $"Spieler {++n}", RegexOptions.IgnoreCase);
        }
        var user = Environment.UserName;
        if (!string.IsNullOrWhiteSpace(user) && user.Length >= 2)
            text = Regex.Replace(text, @"(?<=\\Users\\)" + Regex.Escape(user) + @"(?=\\)", "<Benutzer>", RegexOptions.IgnoreCase);
        return text;
    }
}
