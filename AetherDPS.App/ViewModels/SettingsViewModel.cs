using AetherDPS.App.Services;
using AetherDPS.Engine;
using AetherDPS.Engine.Updates;
using AionDpsMeter.Services.Services.Settings;
using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AetherDPS.App.ViewModels;

public enum HotkeySlot { Meter, Screenshot, Placement }

public sealed record LanguageOption(string Code, string Name)
{
    public override string ToString() => Name;
}

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly MeterSettings meter;
    private readonly IAppSettingsService upstream;
    private HotkeySlot? capturing;

    [ObservableProperty] private string hotkeyText;
    [ObservableProperty] private string screenshotText;
    [ObservableProperty] private string placementText;
    [ObservableProperty] private bool overlaysBySituation;
    [ObservableProperty] private bool isCapturingHotkey;
    [ObservableProperty] private string hotkeyError = "";
    [ObservableProperty] private double opacityPercent;
    [ObservableProperty] private bool partyOnly;
    [ObservableProperty] private bool hideName;
    [ObservableProperty] private bool screenshotEnabled;
    [ObservableProperty] private bool launchGameWithMeter;
    [ObservableProperty] private bool autoUpdate;
    [ObservableProperty] private bool autoTrack;
    [ObservableProperty] private bool combatHints;
    [ObservableProperty] private bool staggerWarning;
    [ObservableProperty] private string updateStatus = "";
    [ObservableProperty] private bool updateReady;
    [ObservableProperty] private LanguageOption selectedLanguage;
    [ObservableProperty] private bool languageChanged;
    [ObservableProperty] private bool usageStats;
    [ObservableProperty] private string discordWebhook = "";
    [ObservableProperty] private string webhookStatus = "";
    [ObservableProperty] private bool discordPresence;
    [ObservableProperty] private bool discordPresenceNames;
    [ObservableProperty] private string presenceStatus = "";

    public IReadOnlyList<LanguageOption> Languages { get; } =
    [
        new("auto", Loc.T("settings.language_auto")),
        new("de", "Deutsch"), new("en", "English"), new("fr", "Français"), new("es", "Español"), new("pt", "Português"),
        new("ru", "Русский"), new("ja", "日本語"), new("ko", "한국어"), new("zh-Hans", "简体中文"), new("zh-Hant", "繁體中文"),
    ];

    /// <summary>Was der Zähler sendet – steht unter dem Schalter.</summary>
    public string UsageStatsExample { get; }

    private readonly UpdateService updates;
    public string VersionText { get; }
    public bool CanSelfUpdate { get; } = AppUpdate.CanSelfUpdate;

    private readonly Engine.Community.DiscordPresence presence;

    public SettingsViewModel(MeterSettings meter, IAppSettingsService upstream, UpdateService updates, Engine.Community.CommunityService community,
        Engine.Community.DiscordPresence presence)
    {
        this.meter = meter;
        this.presence = presence;
        discordPresence = meter.DiscordPresence;
        discordPresenceNames = meter.DiscordPresenceNames;
        presence.StatusChanged += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(RefreshPresence);
        RefreshPresence();
        selectedLanguage = Languages.FirstOrDefault(l => l.Code == meter.Language) ?? Languages[0];
        usageStats = meter.UsageStats == true;
        discordWebhook = meter.DiscordWebhook ?? "";
        UsageStatsExample = Loc.T("settings.usage_stats_example", community.PingExample());
        meter.Changed += (_, _) => UsageStats = meter.UsageStats == true;
        this.upstream = upstream;
        this.updates = updates;
        autoUpdate = meter.AutoUpdate;
        autoTrack = meter.AutoTrack;
        combatHints = meter.CombatHints;
        staggerWarning = meter.StaggerWarning;
        VersionText = Loc.T("update.version", updates.Current);
        updates.Changed += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(RefreshUpdate);
        RefreshUpdate();
        hotkeyText = meter.HotkeyText;
        screenshotText = meter.ScreenshotText;
        placementText = meter.PlacementText;
        overlaysBySituation = meter.OverlaysBySituation;
        opacityPercent = Math.Round(upstream.WindowOpacity * 100);
        partyOnly = meter.PartyOnly;
        hideName = upstream.IsNicknameHidden;
        screenshotEnabled = meter.ScreenshotEnabled;
        launchGameWithMeter = meter.LaunchGameWithMeter;
    }

    partial void OnOpacityPercentChanged(double value) => upstream.WindowOpacity = value / 100.0;

    partial void OnSelectedLanguageChanged(LanguageOption value)
    {
        if (value is null || meter.Language == value.Code) return;
        meter.Language = value.Code;
        meter.Save();
        LanguageChanged = Loc.Resolve(value.Code) != Loc.Language;
    }

    partial void OnDiscordWebhookChanged(string value)
    {
        var url = value.Trim();
        bool valid = Engine.Community.DiscordWebhook.IsValid(url);
        WebhookStatus = url.Length == 0 ? "" : valid ? "" : Loc.T("settings.webhook_invalid");
        var stored = url.Length == 0 ? null : valid ? url : meter.DiscordWebhook;
        if (stored == meter.DiscordWebhook) return;
        meter.DiscordWebhook = stored;
        meter.Save();
    }

    [RelayCommand]
    private async Task TestWebhookAsync()
    {
        if (!Engine.Community.DiscordWebhook.IsValid(DiscordWebhook))
        {
            WebhookStatus = Loc.T("settings.webhook_invalid");
            return;
        }
        WebhookStatus = Loc.T("settings.webhook_testing");
        bool ok = await Engine.Community.DiscordWebhook.SendAsync(DiscordWebhook.Trim(), Loc.T("settings.webhook_test_message"));
        WebhookStatus = Loc.T(ok ? "settings.webhook_ok" : "settings.webhook_failed");
    }

    partial void OnUsageStatsChanged(bool value)
    {
        if (meter.UsageStats == value) return;
        meter.UsageStats = value;
        meter.Save();
    }

    partial void OnDiscordPresenceChanged(bool value)
    {
        if (meter.DiscordPresence == value) return;
        meter.DiscordPresence = value;
        meter.Save();
        RefreshPresence();
    }

    partial void OnDiscordPresenceNamesChanged(bool value)
    {
        if (meter.DiscordPresenceNames == value) return;
        meter.DiscordPresenceNames = value;
        meter.Save();
    }

    private void RefreshPresence() => PresenceStatus = !meter.DiscordPresence ? ""
        : Loc.T(presence.Connected ? "settings.discord_presence_connected" : "settings.discord_presence_waiting");

    /// <summary>Neue Sprache gilt nach einem Neustart.</summary>
    [RelayCommand]
    private void Restart() => AppUpdate.RequestRestart();

    partial void OnPartyOnlyChanged(bool value)
    {
        meter.PartyOnly = value;
        meter.Save();
    }

    partial void OnScreenshotEnabledChanged(bool value)
    {
        meter.ScreenshotEnabled = value;
        meter.Save();
    }

    partial void OnLaunchGameWithMeterChanged(bool value)
    {
        meter.LaunchGameWithMeter = value;
        meter.Save();
    }

    partial void OnOverlaysBySituationChanged(bool value)
    {
        meter.OverlaysBySituation = value;
        meter.Save();
    }

    /// <summary>„Jetzt platzieren“: Platzierungsmodus für alle Overlays starten.</summary>
    public event EventHandler? PlacementRequested;

    [RelayCommand]
    private void PlaceOverlays() => PlacementRequested?.Invoke(this, EventArgs.Empty);

    partial void OnStaggerWarningChanged(bool value)
    {
        meter.StaggerWarning = value;
        meter.Save();
    }

    partial void OnCombatHintsChanged(bool value)
    {
        meter.CombatHints = value;
        meter.Save();
    }

    partial void OnAutoTrackChanged(bool value)
    {
        meter.AutoTrack = value;
        meter.Save();   // TrackingMode schaltet über MeterSettings.Changed um
    }

    partial void OnAutoUpdateChanged(bool value)
    {
        meter.AutoUpdate = value;
        meter.Save();
        if (value && CanSelfUpdate) _ = updates.CheckAndDownloadAsync();
    }

    private void RefreshUpdate()
    {
        UpdateReady = CanSelfUpdate && updates.Pending is not null;
        UpdateStatus = !CanSelfUpdate ? Loc.T("update.dev_build") : updates.State switch
        {
            UpdateState.Checking => Loc.T("update.checking"),
            UpdateState.Downloading => Loc.T("update.downloading", updates.Latest?.Version ?? updates.Current),
            UpdateState.Ready => Loc.T("update.ready", updates.Pending?.Version ?? updates.Current),
            UpdateState.UpToDate => Loc.T("update.up_to_date", updates.LastCheck?.ToString("HH:mm") ?? ""),
            UpdateState.Failed => Loc.T("update.failed", updates.Error ?? ""),
            _ => "",
        };
    }

    [RelayCommand]
    private async Task CheckUpdates()
    {
        if (CanSelfUpdate) await updates.CheckAndDownloadAsync();
    }

    [RelayCommand]
    private void RestartForUpdate() => AppUpdate.RequestRestart();

    [RelayCommand]
    private void OpenReleases() => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
        $"https://github.com/{UpdateService.Repository}/releases") { UseShellExecute = true });

    partial void OnHideNameChanged(bool value) => upstream.IsNicknameHidden = value;

    partial void OnIsCapturingHotkeyChanged(bool value) => GameHotkey.Suspended = value;

    public void BeginCapture(HotkeySlot slot)
    {
        CancelCapture();
        capturing = slot;
        IsCapturingHotkey = true;
        HotkeyError = "";
        SetText(slot, Loc.T("settings.hotkey_press"));
    }

    /// <summary>Taste aus dem Eingabefeld übernehmen. Liefert true, wenn die Kombination gilt.</summary>
    public bool TryCapture(Key key, KeyModifiers modifiers)
    {
        if (capturing is not HotkeySlot slot) return false;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift)
            return false;   // nur Modifier gedrückt – auf die eigentliche Taste warten

        if (HotkeyKeys.ToVirtualKey(key) is not (uint vk, string keyName))
        {
            HotkeyError = Loc.T("settings.hotkey_invalid");
            return false;
        }

        uint mods = 0;
        var parts = new List<string>();
        if (modifiers.HasFlag(KeyModifiers.Control)) { mods |= MeterSettings.ModControl; parts.Add(Loc.T("key.ctrl")); }
        if (modifiers.HasFlag(KeyModifiers.Alt)) { mods |= MeterSettings.ModAlt; parts.Add("ALT"); }
        if (modifiers.HasFlag(KeyModifiers.Shift)) { mods |= MeterSettings.ModShift; parts.Add("SHIFT"); }

        // Ohne Modifier nur F-Tasten erlauben – sonst wäre z. B. „W“ allein im Spiel weg.
        if (mods == 0 && key is not (>= Key.F1 and <= Key.F12))
        {
            HotkeyError = Loc.T("settings.hotkey_invalid");
            return false;
        }

        parts.Add(keyName);
        var text = string.Join("+", parts);
        var others = new List<(uint, uint)>();
        if (slot != HotkeySlot.Meter) others.Add((meter.HotkeyModifiers, meter.HotkeyKey));
        if (slot != HotkeySlot.Screenshot) others.Add((meter.ScreenshotModifiers, meter.ScreenshotKey));
        if (slot != HotkeySlot.Placement) others.Add((meter.PlacementModifiers, meter.PlacementKey));
        if (others.Contains((mods, vk)))
        {
            HotkeyError = Loc.T("settings.hotkey_taken");
            return false;
        }
        Apply(slot, mods, vk, text);
        return true;
    }

    public void CancelCapture()
    {
        if (capturing is not HotkeySlot slot) return;
        capturing = null;
        IsCapturingHotkey = false;
        SetText(slot, slot switch
        {
            HotkeySlot.Meter => meter.HotkeyText,
            HotkeySlot.Screenshot => meter.ScreenshotText,
            _ => meter.PlacementText,
        });
    }

    [RelayCommand]
    private void ResetHotkey() => Apply(HotkeySlot.Meter, MeterSettings.ModControl, 0x57, Loc.T("key.ctrl") + "+W");

    [RelayCommand]
    private void ResetScreenshotHotkey() => Apply(HotkeySlot.Screenshot, MeterSettings.ModControl, 0x53, Loc.T("key.ctrl") + "+S");

    [RelayCommand]
    private void ResetPlacementHotkey() => Apply(HotkeySlot.Placement, MeterSettings.ModControl, 0x50, Loc.T("key.ctrl") + "+P");

    private void SetText(HotkeySlot slot, string text)
    {
        if (slot == HotkeySlot.Meter) HotkeyText = text;
        else if (slot == HotkeySlot.Screenshot) ScreenshotText = text;
        else PlacementText = text;
    }

    private void Apply(HotkeySlot slot, uint mods, uint vk, string text)
    {
        if (slot == HotkeySlot.Meter)
        {
            meter.HotkeyModifiers = mods;
            meter.HotkeyKey = vk;
            meter.HotkeyText = text;
        }
        else if (slot == HotkeySlot.Screenshot)
        {
            meter.ScreenshotModifiers = mods;
            meter.ScreenshotKey = vk;
            meter.ScreenshotText = text;
        }
        else
        {
            meter.PlacementModifiers = mods;
            meter.PlacementKey = vk;
            meter.PlacementText = text;
        }
        meter.Save();
        capturing = null;
        IsCapturingHotkey = false;
        HotkeyError = "";
        SetText(slot, text);
    }
}

public static class HotkeyKeys
{
    /// <summary>Avalonia-Taste → Win32-Tastencode. Nur Buchstaben, Ziffern und F-Tasten.</summary>
    public static (uint Vk, string Name)? ToVirtualKey(Key key) => key switch
    {
        >= Key.A and <= Key.Z => ((uint)('A' + (key - Key.A)), ((char)('A' + (key - Key.A))).ToString()),
        >= Key.D0 and <= Key.D9 => ((uint)('0' + (key - Key.D0)), ((char)('0' + (key - Key.D0))).ToString()),
        >= Key.F1 and <= Key.F12 => ((uint)(0x70 + (key - Key.F1)), $"F{key - Key.F1 + 1}"),
        _ => null,
    };
}
