using Aion2Meter.App.Services;
using Aion2Meter.Engine;
using AionDpsMeter.Services.Services.Settings;
using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aion2Meter.App.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly MeterSettings meter;
    private readonly IAppSettingsService upstream;

    [ObservableProperty] private string hotkeyText;
    [ObservableProperty] private bool isCapturingHotkey;
    [ObservableProperty] private string hotkeyError = "";
    [ObservableProperty] private double opacityPercent;
    [ObservableProperty] private bool partyOnly;
    [ObservableProperty] private bool hideName;

    public SettingsViewModel(MeterSettings meter, IAppSettingsService upstream)
    {
        this.meter = meter;
        this.upstream = upstream;
        hotkeyText = meter.HotkeyText;
        opacityPercent = Math.Round(upstream.WindowOpacity * 100);
        partyOnly = meter.PartyOnly;
        hideName = upstream.IsNicknameHidden;
    }

    partial void OnOpacityPercentChanged(double value) => upstream.WindowOpacity = value / 100.0;

    partial void OnPartyOnlyChanged(bool value)
    {
        meter.PartyOnly = value;
        meter.Save();
    }

    partial void OnHideNameChanged(bool value) => upstream.IsNicknameHidden = value;

    partial void OnIsCapturingHotkeyChanged(bool value) => GameHotkey.Suspended = value;

    public void BeginCapture()
    {
        IsCapturingHotkey = true;
        HotkeyError = "";
        HotkeyText = Loc.T("settings.hotkey_press");
    }

    /// <summary>Taste aus dem Eingabefeld übernehmen. Liefert true, wenn die Kombination gilt.</summary>
    public bool TryCapture(Key key, KeyModifiers modifiers)
    {
        if (!IsCapturingHotkey) return false;
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
        Apply(mods, vk, string.Join("+", parts));
        return true;
    }

    public void CancelCapture()
    {
        if (!IsCapturingHotkey) return;
        IsCapturingHotkey = false;
        HotkeyText = meter.HotkeyText;
    }

    [RelayCommand]
    private void ResetHotkey() => Apply(MeterSettings.ModControl, 0x57, "STRG+W");

    private void Apply(uint mods, uint vk, string text)
    {
        meter.HotkeyModifiers = mods;
        meter.HotkeyKey = vk;
        meter.HotkeyText = text;
        meter.Save();
        IsCapturingHotkey = false;
        HotkeyError = "";
        HotkeyText = text;
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
