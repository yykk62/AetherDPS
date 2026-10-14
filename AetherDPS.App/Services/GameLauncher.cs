using System.Diagnostics;

namespace AetherDPS.App.Services;

/// <summary>
/// AION 2 über Steam starten – läuft der Meter schon beim Login, kennt er deinen Charakter
/// sofort (das Spiel schickt Name und ID nur bei Login/Teleport).
/// </summary>
public static class GameLauncher
{
    public const int SteamAppId = 3393110;
    private const string ProcessName = "AION2";

    public static bool IsRunning => Process.GetProcessesByName(ProcessName).Length > 0;

    /// <summary>Startzeit des laufenden Spiels (null, wenn es nicht läuft oder nicht lesbar ist).</summary>
    public static DateTime? StartedAt()
    {
        foreach (var p in Process.GetProcessesByName(ProcessName))
        {
            using (p)
            {
                try
                {
                    return p.StartTime;
                }
                catch (Exception)
                {
                    // geschützter Prozess – nächsten versuchen
                }
            }
        }
        return null;
    }

    public static void Launch() =>
        Process.Start(new ProcessStartInfo($"steam://rungameid/{SteamAppId}") { UseShellExecute = true });
}
