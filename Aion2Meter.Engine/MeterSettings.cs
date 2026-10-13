using System.Text.Json;

namespace Aion2Meter.Engine;

/// <summary>
/// Unsere eigenen Einstellungen (aion2meter.json im Datenordner). Deckkraft und
/// „Du statt Name“ liegen in den Upstream-Einstellungen (appsettings.user.json).
/// </summary>
public sealed class MeterSettings
{
    public const uint ModAlt = 0x1, ModControl = 0x2, ModShift = 0x4;

    /// <summary>Nur Mitglieder der eigenen Gruppe anzeigen (offene Welt).</summary>
    public bool PartyOnly { get; set; } = true;

    /// <summary>Win32-Modifier (MOD_CONTROL usw.) und virtueller Tastencode. Standard: STRG+W.</summary>
    public uint HotkeyModifiers { get; set; } = ModControl;
    public uint HotkeyKey { get; set; } = 0x57;
    public string HotkeyText { get; set; } = "STRG+W";

    /// <summary>Screenshot des Spiel-Bildschirms, Vollbild auf dem Zweitbildschirm. Standard: STRG+S.</summary>
    public bool ScreenshotEnabled { get; set; } = true;
    public uint ScreenshotModifiers { get; set; } = ModControl;
    public uint ScreenshotKey { get; set; } = 0x53;
    public string ScreenshotText { get; set; } = "STRG+S";

    /// <summary>Buff-/Abklingzeit-Leiste unter dem Meter (nur im aktiven Modus sichtbar).</summary>
    public bool BuffBarEnabled { get; set; } = true;

    private string? path;

    public event EventHandler? Changed;

    public static MeterSettings Load(string dataDir)
    {
        var file = Path.Combine(dataDir, "aion2meter.json");
        MeterSettings settings;
        try
        {
            settings = File.Exists(file)
                ? JsonSerializer.Deserialize<MeterSettings>(File.ReadAllText(file)) ?? new MeterSettings()
                : new MeterSettings();
        }
        catch (JsonException)
        {
            settings = new MeterSettings();
        }
        settings.path = file;
        return settings;
    }

    public void Save()
    {
        if (path is not null)
            File.WriteAllText(path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
