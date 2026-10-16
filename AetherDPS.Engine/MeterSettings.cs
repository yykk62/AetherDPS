using System.Text.Json;

namespace AetherDPS.Engine;

/// <summary>
/// Unsere eigenen Einstellungen (aetherdps.json im Datenordner). Deckkraft und
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


    /// <summary>Zuletzt erkannter eigener Charakter – damit der Name auch ohne Teleport stimmt.</summary>
    public string? CharacterName { get; set; }
    public string? CharacterServer { get; set; }
    public int CharacterClassId { get; set; }

    /// <summary>Tritt jemand der Gruppe bei, sein Profil (Kampfkraft, Itemstufe, Waffe) kurz einblenden – lädt bei NCSOFT.</summary>
    public bool PartyJoinPopup { get; set; } = true;

    /// <summary>Pet-Radar (fehlende Pets in der Nähe): „map“ = solange die Spielkarte offen ist, „always“ = dauerhaft, „off“. Position in Bildschirm-Pixeln.</summary>
    public string PetRadarMode { get; set; } = "map";

    /// <summary>Welche Pets Pet-Finder und Radar zeigen: „all“, „missing“ (noch nicht gesammelt), „open“ (nicht auf Höchststufe).</summary>
    public string PetFilter { get; set; } = "open";

    /// <summary>Gattung im Pet-Finder (null = alle) – gilt auch fürs Radar.</summary>
    public string? PetSpecies { get; set; }

    /// <summary>Liste unter der Radar-Karte aufgeklappt.</summary>
    public bool PetRadarList { get; set; } = true;
    public double? PetRadarLeft { get; set; }
    public double? PetRadarTop { get; set; }

    /// <summary>Zoom des Pet-Radars: sichtbarer Umkreis in Metern (null = Standard 600 m).</summary>
    public double? PetRadarMeters { get; set; }

    /// <summary>Größe des Pet-Radars (Breite und Kartenhöhe in DIP; null = so groß wie die Spielkarte, mittig).</summary>
    public double? PetRadarWidth { get; set; }
    public double? PetRadarMapHeight { get; set; }

    /// <summary>Pet-Radar eingeklappt: nur eine kleine Leiste, Klick klappt es wieder auf.</summary>
    public bool PetRadarCollapsed { get; set; }

    /// <summary>Session-ID des Charakters beim letzten Login/Teleport – gilt, solange das Spiel nicht neu gestartet wurde.</summary>
    public int CharacterSessionId { get; set; }
    public DateTime? CharacterSeenAt { get; set; }

    /// <summary>AION 2 über Steam mitstarten, wenn der Meter startet (und das Spiel noch nicht läuft).</summary>
    public bool LaunchGameWithMeter { get; set; }

    /// <summary>Servername, den du selbst eingetragen hast (EU-Server fehlen in den Spieldaten).</summary>
    public string? ServerName { get; set; }

    /// <summary>
    /// Automatisch erfassen: Jeder Bosskampf startet die Aufzeichnung von selbst, ohne Hotkey.
    /// Das Overlay bleibt dann als schmale Leiste sichtbar und klappt im Kampf auf.
    /// </summary>
    public bool AutoTrack { get; set; }

    /// <summary>Kampf-Hinweise: Stagger-Phase (Ankündigung + Ergebnis), Todes-Rückblick, auslaufende Buffs.</summary>
    public bool CombatHints { get; set; } = true;

    /// <summary>Große, halbtransparente Stagger-Warnung mitten im Bild (Restzeit + Leiste). Standard: aus.</summary>
    public bool StaggerWarning { get; set; }

    /// <summary>Neue Versionen von GitHub laden (geprüft per SHA-256) und beim nächsten Start installieren.</summary>
    public bool AutoUpdate { get; set; } = true;

    /// <summary>Sprache der Oberfläche und Spieltexte („auto“ = wie Windows). Gilt nach Neustart.</summary>
    public string Language { get; set; } = "auto";

    /// <summary>Anonymer Nutzungszähler: null = noch nicht gefragt, sonst die Antwort.</summary>
    public bool? UsageStats { get; set; }

    /// <summary>UTC-Tag des letzten Zähler-Signals („2026-10-15“) – höchstens eins pro Tag.</summary>
    public string? LastPingDay { get; set; }

    /// <summary>Version beim letzten Start – ist sie neuer, zeigt der Meter „Was ist neu“.</summary>
    public string? LastSeenVersion { get; set; }

    /// <summary>Discord-Webhook für „An Discord senden“ bei Läufen (leer = aus).</summary>
    public string? DiscordWebhook { get; set; }

    /// <summary>Im Discord-Profil zeigen, was du machst (Rich Presence, nur lokal über den Discord-Client). Standard: aus.</summary>
    public bool DiscordPresence { get; set; }

    /// <summary>Rich Presence: Dungeon- und Bossnamen mit anzeigen (sonst nur „Im Dungeon“/„Bosskampf“).</summary>
    public bool DiscordPresenceNames { get; set; } = true;

    /// <summary>Einmal im Hauptfenster gefragt, ob die Rich Presence an soll (danach nie wieder).</summary>
    public bool DiscordPresenceOffered { get; set; }

    /// <summary>Schon angezeigte Nachrichten an alle (Ids aus status.json).</summary>
    public List<string> SeenMessages { get; set; } = [];

    private string? path;

    public event EventHandler? Changed;

    public static MeterSettings Load(string dataDir)
    {
        var file = Path.Combine(dataDir, "aetherdps.json");
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
        // Tastennamen in der Sprache der Oberfläche („STRG+W“ ↔ „CTRL+W“)
        settings.HotkeyText = LocalizeKeys(settings.HotkeyText);
        settings.ScreenshotText = LocalizeKeys(settings.ScreenshotText);
        return settings;
    }

    private static string LocalizeKeys(string text)
    {
        var parts = text.Split('+');
        if (parts.Length > 1 && parts[0] is "STRG" or "CTRL") parts[0] = Loc.T("key.ctrl");
        return string.Join('+', parts);
    }

    public void Save()
    {
        try
        {
            if (path is not null)
                File.WriteAllText(path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        }
        catch (IOException)
        {
            // z. B. kurz von einem Virenscanner gesperrt – beim nächsten Speichern klappt es
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
