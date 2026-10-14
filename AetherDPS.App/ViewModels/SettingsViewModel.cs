using AetherDPS.App.Services;
using AetherDPS.Engine;
using AetherDPS.Engine.Updates;
using AionDpsMeter.Services.Services.Settings;
using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AetherDPS.App.ViewModels;

public enum HotkeySlot { Meter, Screenshot }

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly MeterSettings meter;
    private readonly IAppSettingsService upstream;
    private HotkeySlot? capturing;

    [ObservableProperty] private string hotkeyText;
    [ObservableProperty] private string screenshotText;
    [ObservableProperty] private bool isCapturingHotkey;
    [ObservableProperty] private string hotkeyError = "";
    [ObservableProperty] private double opacityPercent;
    [ObservableProperty] private bool partyOnly;
    [ObservableProperty] private bool hideName;
    [ObservableProperty] private bool screenshotEnabled;
    [ObservableProperty] private bool buffBarEnabled;
    [ObservableProperty] private bool launchGameWithMeter;
    [ObservableProperty] private bool autoUpdate;
    [ObservableProperty] private bool autoTrack;
    [ObservableProperty] private bool combatHints;
    [ObservableProperty] private bool staggerWarning;
    [ObservableProperty] private string updateStatus = "";
    [ObservableProperty] private bool updateReady;

    private readonly UpdateService updates;
    public string VersionText { get; }
    public bool CanSelfUpdate { get; } = AppUpdate.CanSelfUpdate;

    public SettingsViewModel(MeterSettings meter, IAppSettingsService upstream, UpdateService updates)
    {
        this.meter = meter;
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
        opacityPercent = Math.Round(upstream.WindowOpacity * 100);
        partyOnly = meter.PartyOnly;
        hideName = upstream.IsNicknameHidden;
        screenshotEnabled = meter.ScreenshotEnabled;
        buffBarEnabled = meter.BuffBarEnabled;
        launchGameWithMeter = meter.LaunchGameWithMeter;
    }

    partial void OnOpacityPercentChanged(double value) => upstream.WindowOpacity = value / 100.0;

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

    partial void OnBuffBarEnabledChanged(bool value)
    {
        meter.BuffBarEnabled = value;
        meter.Save();
    }

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
        if (modifiers.HasFlag(KeyModifiers.Control)) { mods |= MeterSettings.ModControl; parts.Add("STRG"); }
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
        var other = slot == HotkeySlot.Meter
            ? (meter.ScreenshotModifiers, meter.ScreenshotKey)
            : (meter.HotkeyModifiers, meter.HotkeyKey);
        if (other == (mods, vk))
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
        SetText(slot, slot == HotkeySlot.Meter ? meter.HotkeyText : meter.ScreenshotText);
    }

    [RelayCommand]
    private void ResetHotkey() => Apply(HotkeySlot.Meter, MeterSettings.ModControl, 0x57, "STRG+W");

    [RelayCommand]
    private void ResetScreenshotHotkey() => Apply(HotkeySlot.Screenshot, MeterSettings.ModControl, 0x53, "STRG+S");

    private void SetText(HotkeySlot slot, string text)
    {
        if (slot == HotkeySlot.Meter) HotkeyText = text;
        else ScreenshotText = text;
    }

    private void Apply(HotkeySlot slot, uint mods, uint vk, string text)
    {
        if (slot == HotkeySlot.Meter)
        {
            meter.HotkeyModifiers = mods;
            meter.HotkeyKey = vk;
            meter.HotkeyText = text;
        }
        else
        {
            meter.ScreenshotModifiers = mods;
            meter.ScreenshotKey = vk;
            meter.ScreenshotText = text;
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
