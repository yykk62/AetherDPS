using System.Text.RegularExpressions;
using AetherDPS.App.ViewModels;
using AetherDPS.Engine;
using AetherDPS.Engine.Community;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;

namespace AetherDPS.App.Services;

/// <summary>
/// Hinweiskarten und Netz-Kontakt rund um die Community: Zustimmung zum Zähler, täglicher Zähler,
/// Absturzbericht, „Was ist neu“, Nachrichten an alle (status.json) und Spiel-Update erkannt.
/// </summary>
public sealed partial class CommunityController(
    CommunityService community, MeterSettings settings, MeterOptions options, PatchDetector patch, ReportViewModel report)
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

    private HubViewModel? hub;
    private Action<string, string>? toast;

    /// <summary>Hauptfenster und Einblendungen verbinden (ohne Netz – auch für Tests).</summary>
    internal void Attach(HubViewModel hub, Action<string, string> toast)
    {
        this.hub = hub;
        this.toast = toast;
    }

    public void Start(HubViewModel hub, Action<string, string> toast, bool askConsent)
    {
        Attach(hub, toast);
        ShowWhatsNew();
        ShowCrash();
        if (askConsent && settings.UsageStats is null) ShowConsent();
        // Erst beim nächsten Start nach der Zustimmungsfrage, damit nicht zwei Fragen auf einmal kommen
        else if (askConsent && !settings.DiscordPresence && !settings.DiscordPresenceOffered) ShowPresenceOffer();

        patch.Changed += broken => Dispatcher.UIThread.Post(() => OnPatchChanged(broken));
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(15));
            while (true)
            {
                var status = await community.RefreshStatusAsync();
                if (status is not null) Dispatcher.UIThread.Post(() => ShowStatus(status));
                await community.PingIfDueAsync();
                await Task.Delay(Interval);
            }
        });
    }

    private Version Current => typeof(CommunityController).Assembly.GetName().Version ?? new Version(0, 0, 0);

    // ---------- Zustimmung ----------

    internal void ShowConsent() => hub?.ShowNotice(new HubNotice
    {
        Id = "consent",
        Icon = "shield-check",
        Title = Loc.T("consent.title"),
        Text = Loc.T("consent.text"),
        Detail = Loc.T("consent.example", community.PingExample()),
        PrimaryText = Loc.T("consent.yes"),
        Primary = new RelayCommand(() => AnswerConsent(true)),
        SecondaryText = Loc.T("consent.no"),
        Secondary = new RelayCommand(() => AnswerConsent(false)),
    });

    private void AnswerConsent(bool yes)
    {
        settings.UsageStats = yes;
        settings.Save();
        hub?.RemoveNotice("consent");
        if (yes) _ = Task.Run(() => community.PingIfDueAsync());
    }

    // ---------- Discord-Profil (einmal fragen) ----------

    internal void ShowPresenceOffer() => hub?.ShowNotice(new HubNotice
    {
        Id = "presence",
        Icon = "chat",
        Title = Loc.T("presence_offer.title"),
        Text = Loc.T("presence_offer.text"),
        PrimaryText = Loc.T("presence_offer.yes"),
        Primary = new RelayCommand(() => AnswerPresence(true)),
        SecondaryText = Loc.T("presence_offer.no"),
        Secondary = new RelayCommand(() => AnswerPresence(false)),
    });

    private void AnswerPresence(bool yes)
    {
        settings.DiscordPresenceOffered = true;
        if (yes) settings.DiscordPresence = true;
        settings.Save();
        hub?.RemoveNotice("presence");
    }

    // ---------- Absturz ----------

    private void ShowCrash()
    {
        if (ReportText.TakeCrash(options.DataDir) is not { } crash) return;
        hub?.ShowNotice(new HubNotice
        {
            Id = "crash",
            Icon = "bug",
            Title = Loc.T("crash.title"),
            Text = Loc.T("crash.text"),
            IsWarning = true,
            PrimaryText = Loc.T("crash.send"),
            Primary = new AsyncRelayCommand(() => SendCrashAsync(crash)),
            SecondaryText = Loc.T("crash.skip"),
            Secondary = new RelayCommand(() => hub?.RemoveNotice("crash")),
        });
    }

    private async Task SendCrashAsync(string crash)
    {
        var names = report.KnownNames().ToList();
        var id = await community.SendReportAsync("crash", ReportText.Scrub(crash, names), report.CleanLog());
        hub?.ShowNotice(new HubNotice
        {
            Id = "crash",
            Icon = id is null ? "warning" : "check",
            Title = Loc.T("crash.title"),
            Text = id is null ? Loc.T("report.failed") : Loc.T("report.sent", id),
            IsWarning = id is null,
        });
    }

    // ---------- Was ist neu ----------

    private void ShowWhatsNew()
    {
        var current = $"{Current.Major}.{Current.Minor}.{Math.Max(0, Current.Build)}";
        var last = settings.LastSeenVersion;
        settings.LastSeenVersion = current;
        settings.Save();
        // Erster Start überhaupt: nichts „Neues“ zeigen
        if (last is null || !Version.TryParse(last, out var lastVersion) || lastVersion >= Version.Parse(current)) return;
        if (ChangelogSection(current) is not { } text) return;
        hub?.ShowNotice(new HubNotice { Id = "whatsnew", Icon = "scroll", Title = Loc.T("whatsnew.title", current), Text = text, IsExpanded = false });
    }

    /// <summary>Abschnitt „## 0.6.0“ aus dem eingebetteten CHANGELOG (Englisch für alle außer Deutsch).</summary>
    internal static string? ChangelogSection(string version)
    {
        var name = Loc.Language == "de" ? "CHANGELOG.md" : "CHANGELOG.en.md";
        using var stream = typeof(CommunityController).Assembly.GetManifestResourceStream(name);
        if (stream is null) return null;
        var lines = new StreamReader(stream).ReadToEnd().Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        int start = lines.FindIndex(l => l.StartsWith("## ") && l[3..].Trim('[', ']', ' ') == version);
        if (start < 0) return null;
        var section = lines.Skip(start + 1).TakeWhile(l => !l.StartsWith("## ")).Where(l => l.Trim().Length > 0);
        var text = string.Join("\n", section.Select(l => l.StartsWith("- ") ? "• " + l[2..] : l));
        return Markdown().Replace(text, "$1").Trim();
    }

    [GeneratedRegex(@"\*\*(.+?)\*\*")]
    private static partial Regex Markdown();

    // ---------- Nachrichten an alle ----------

    private void ShowStatus(CommunityStatus status)
    {
        if (hub is null) return;
        hub.DiscordUrl = status.Discord;
        foreach (var old in hub.Notices.Where(n => n.Id.StartsWith("msg:", StringComparison.Ordinal)).ToList())
            if (status.Messages.All(m => "msg:" + m.Id != old.Id)) hub.RemoveNotice(old.Id);
        foreach (var m in status.Messages)
        {
            bool warn = m.Level is "warn" or "warning";
            if (hub.Notices.All(n => n.Id != "msg:" + m.Id))
                hub.ShowNotice(new HubNotice { Id = "msg:" + m.Id, Icon = warn ? "warning" : "megaphone", Title = Loc.T(warn ? "msg.title_warn" : "msg.title"), Text = m.Text, IsWarning = warn });
            if (!settings.SeenMessages.Contains(m.Id))
            {
                settings.SeenMessages.Add(m.Id);
                if (settings.SeenMessages.Count > 50) settings.SeenMessages.RemoveAt(0);
                settings.Save();
                toast?.Invoke(Loc.T(warn ? "msg.title_warn" : "msg.title"), m.Text);
            }
        }
    }

    // ---------- Spiel-Update erkannt ----------

    private void OnPatchChanged(bool broken)
    {
        if (!broken)
        {
            hub?.RemoveNotice("patch");
            return;
        }
        Serilog.Log.Warning("Patch-Erkennung: viele Pakete, aber keins der Grundpakete – Spiel-Update?");
        hub?.ShowNotice(new HubNotice { Id = "patch", Icon = "tools", Title = Loc.T("patch.title"), Text = Loc.T("patch.text"), IsWarning = true });
        toast?.Invoke(Loc.T("patch.title"), Loc.T("patch.toast"));
    }
}
