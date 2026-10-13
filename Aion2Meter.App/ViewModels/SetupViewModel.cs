using System.Diagnostics;
using Aion2Meter.App.Services;
using Aion2Meter.Engine;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace Aion2Meter.App.ViewModels;

public sealed partial class SetupViewModel(ILogger<SetupViewModel> logger) : ObservableObject
{
    [ObservableProperty] private string stateText = "";
    [ObservableProperty] private double progress;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool hasError;
    [ObservableProperty] private bool isDone = Npcap.IsInstalled();
    [ObservableProperty] private string buttonText = Loc.T("setup.button");
    [ObservableProperty] private int activeStep = 1;

    /// <summary>Wird ausgelöst, sobald Npcap installiert ist.</summary>
    public event EventHandler? Completed;

    public void Refresh()
    {
        IsDone = Npcap.IsInstalled();
        if (IsDone && !IsBusy)
        {
            HasError = false;
            ActiveStep = 3;
            StateText = Loc.T("setup.already");
        }
    }

    [RelayCommand]
    private async Task InstallAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        HasError = false;
        Progress = 0;
        var reporter = new Progress<SetupProgress>(p =>
        {
            Progress = p.Percent;
            (ActiveStep, StateText) = p.Step switch
            {
                SetupStep.Download => (1, Loc.T("setup.state.download", Npcap.Version, Format.Percent(p.Percent))),
                SetupStep.Verify => (1, Loc.T("setup.state.verify")),
                SetupStep.Install => (2, Loc.T("setup.state.install")),
                _ => (3, Loc.T("setup.state.done")),
            };
        });

        try
        {
            var signer = await NpcapSetup.RunAsync(reporter);
            logger.LogInformation("Npcap {Version} installiert, signiert von {Signer}", Npcap.Version, signer);
            IsDone = true;
            Completed?.Invoke(this, EventArgs.Empty);
        }
        catch (NpcapSetupException ex)
        {
            logger.LogWarning(ex, "Npcap-Einrichtung: {Key} {Detail}", ex.MessageKey, ex.Detail);
            Fail(ex.MessageKey);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Npcap-Einrichtung unerwartet fehlgeschlagen");
            Fail("setup.error.not_installed");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Fail(string key)
    {
        HasError = true;
        StateText = Loc.T(key);
        ButtonText = Loc.T("setup.button_retry");
    }

    [RelayCommand]
    private void StartMeter() => Completed?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private static void OpenOfficialPage() =>
        Process.Start(new ProcessStartInfo(Npcap.DownloadPage) { UseShellExecute = true });
}
