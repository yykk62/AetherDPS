using Aion2Meter.App.Services;
using Aion2Meter.Engine;
using Aion2Meter.Engine.Capture;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Aion2Meter.App.ViewModels;

/// <summary>Geführte Aufnahme: zeigt den aktuellen Schritt, F8 oder „Erledigt“ setzt die Marke.</summary>
public sealed partial class ResearchViewModel : ObservableObject
{
    private readonly ResearchRecorder recorder;
    private readonly MeterSettings settings;
    private readonly DispatcherTimer timer;

    [ObservableProperty] private string progress = "";
    [ObservableProperty] private string stats = "";
    [ObservableProperty] private string task = "";
    [ObservableProperty] private string why = "";
    [ObservableProperty] private bool asksText;
    [ObservableProperty] private string answer = "";

    /// <summary>Aufnahme ist zu Ende (fertig oder abgebrochen) – die App schließt das Fenster.</summary>
    public event EventHandler? Finished;

    public ResearchViewModel(ResearchRecorder recorder, MeterSettings settings)
    {
        this.recorder = recorder;
        this.settings = settings;
        recorder.Changed += (_, _) => Dispatcher.UIThread.Post(Refresh);
        timer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => UpdateStats());
    }

    public void Begin(string dataDir)
    {
        recorder.Start(Path.Combine(dataDir, "Mitschnitte"), typeof(ResearchViewModel).Assembly.GetName().Version?.ToString() ?? "?");
        Answer = settings.ServerName ?? "";
        timer.Start();
        Refresh();
    }

    private void Refresh()
    {
        if (recorder.CurrentStep is not { } step)
        {
            timer.Stop();
            Finished?.Invoke(this, EventArgs.Empty);
            return;
        }
        Progress = Loc.T("research.progress", recorder.StepIndex + 1, ResearchRecorder.Steps.Count);
        Task = step.Task;
        Why = Loc.T("research.why", step.Why);
        AsksText = step.AsksText;
        UpdateStats();
    }

    private void UpdateStats()
    {
        var elapsed = DateTime.Now - recorder.StartedAt;
        Stats = $"{(int)elapsed.TotalMinutes}:{elapsed.Seconds:00} · {recorder.Bytes / 1_048_576.0:0.0} MB";
    }

    /// <summary>Auch per F8 (GameHotkey).</summary>
    [RelayCommand]
    public void Done()
    {
        if (recorder.CurrentStep is { AsksText: true } && !string.IsNullOrWhiteSpace(Answer))
        {
            settings.ServerName = Answer.Trim();
            settings.Save();
        }
        recorder.Next(done: true, AsksText ? Answer.Trim() : null);
    }

    [RelayCommand]
    private void Skip() => recorder.Next(done: false);

    [RelayCommand]
    private void Stop() => recorder.Stop();
}
