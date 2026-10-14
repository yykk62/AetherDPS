using AetherDPS.Engine;
using AetherDPS.Engine.Community;
using AionDpsMeter.Services.Services.Entity;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AetherDPS.App.ViewModels;

/// <summary>
/// „Problem melden“: Beschreibung + (auf Wunsch) das Log, Namen von Spielern entfernt, direkt an den
/// Entwickler. Was gesendet wird, ist vorher im Fenster zu sehen.
/// </summary>
public sealed partial class ReportViewModel : ObservableObject
{
    private readonly CommunityService community;
    private readonly MeterOptions options;
    private readonly MeterSettings settings;
    private readonly EntityTracker entities;
    private readonly PartyRoster roster;

    [ObservableProperty] private string message = "";
    [ObservableProperty] private bool attachLog = true;
    [ObservableProperty] private string logPreview = "";
    [ObservableProperty] private bool isSending;
    [ObservableProperty] private bool isSent;
    [ObservableProperty] private string status = "";
    [ObservableProperty] private bool statusIsError;

    public ReportViewModel(CommunityService community, MeterOptions options, MeterSettings settings, EntityTracker entities, PartyRoster roster)
    {
        this.community = community;
        this.options = options;
        this.settings = settings;
        this.entities = entities;
        this.roster = roster;
    }

    public event EventHandler? CloseRequested;

    /// <summary>Beim Öffnen: Felder leeren und das bereinigte Log laden.</summary>
    public void Prepare()
    {
        Message = "";
        AttachLog = true;
        IsSent = false;
        Status = "";
        StatusIsError = false;
        LogPreview = CleanLog() ?? Loc.T("report.no_log");
    }

    public string? CleanLog() => ReportText.ReadLatestLog(options.DataDir) is { } log ? ReportText.Scrub(log, KnownNames()) : null;

    /// <summary>Alle Namen, die der Meter kennt: eigener Charakter, Gruppe, Spieler der Sitzung.</summary>
    public IEnumerable<string?> KnownNames() =>
        new[] { settings.CharacterName }
            .Concat(roster.Members.Select(m => (string?)m.Name))
            .Concat(entities.PlayerEntities.Select(p => (string?)p.Name));

    [RelayCommand]
    private async Task SendAsync()
    {
        if (IsSending || IsSent) return;
        if (string.IsNullOrWhiteSpace(Message) && !AttachLog)
        {
            Status = Loc.T("report.need_text");
            StatusIsError = true;
            return;
        }
        IsSending = true;
        Status = Loc.T("report.sending");
        StatusIsError = false;
        var id = await community.SendReportAsync("manual", Message.Trim(), AttachLog ? CleanLog() : null);
        IsSending = false;
        if (id is null)
        {
            Status = Loc.T("report.failed");
            StatusIsError = true;
            return;
        }
        IsSent = true;
        Status = Loc.T("report.sent", id);
    }

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke(this, EventArgs.Empty);
}
