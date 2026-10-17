using AetherDPS.App.Services;
using AetherDPS.Engine;

namespace AetherDPS.App.ViewModels;

/// <summary>Ein Boss im Lauf: ab wann, wie lange, Gruppen-DPS, gelegt oder nicht, Hinweis (Versuche, Erfassung).</summary>
public sealed record BossLine(string Name, string From, string Duration, string GroupDps, bool Killed, string Note = "");

/// <summary>
/// Ein Lauf im Vergleich zu deinen früheren Läufen mit demselben Namen: Urteil („Neue Bestzeit,
/// 8 % mehr DPS als sonst“), Werte für die Messlatten, Platz in der Gruppe, Boss-Zeilen.
/// Gemeinsam für „Jetzt“ und „Kämpfe“, damit beide dieselben Worte und Formate benutzen.
/// </summary>
public sealed class RunComparison
{
    public ArchivedPlayer? User { get; }
    public IReadOnlyList<double> DpsHistory { get; }
    public double DpsAverage { get; }
    public double DpsBest { get; }
    public IReadOnlyList<double> TimeHistory { get; }
    public double TimeAverage { get; }
    public double TimeBest { get; }
    public bool IsBestTime { get; }
    public int Rank { get; }
    public int PartySize { get; }
    public int BossCount { get; }
    public int BossesKilled { get; }
    public string Verdict { get; }

    public RunComparison(ArchivedRun run, IReadOnlyList<ArchivedRun> all)
    {
        var earlier = all.Where(r => r.Name == run.Name && r.Id != run.Id && r.StartedAt < run.StartedAt).ToList();
        User = run.Players.FirstOrDefault(p => p.IsUser);
        DpsHistory = earlier.Select(r => r.Players.FirstOrDefault(p => p.IsUser)?.Dps).OfType<double>().ToList();
        DpsAverage = DpsHistory.Count > 0 ? DpsHistory.Average() : double.NaN;
        DpsBest = DpsHistory.Count > 0 ? DpsHistory.Max() : double.NaN;

        var bosses = Significant(run);
        BossCount = bosses.Count;
        BossesKilled = bosses.Count(b => b.Killed);
        var complete = earlier.Where(r => r.BossesKilled > 0 && r.BossesKilled == r.Bosses.Count).ToList();
        TimeHistory = complete.Select(r => r.DurationSeconds).ToList();
        TimeAverage = TimeHistory.Count > 0 ? TimeHistory.Average() : double.NaN;
        TimeBest = TimeHistory.Count > 0 ? TimeHistory.Min() : double.NaN;
        IsBestTime = TimeHistory.Count > 0 && BossCount > 0 && BossesKilled == BossCount && run.DurationSeconds < TimeBest;

        var ranked = run.Players.OrderByDescending(p => p.Damage).ToList();
        PartySize = ranked.Count;
        Rank = User is null ? 0 : ranked.IndexOf(User) + 1;

        var parts = new List<string>();
        if (TimeHistory.Count > 0)
            parts.Add(IsBestTime ? Loc.T("now.verdict_best")
                : Loc.T("now.verdict_behind", Dur(TimeSpan.FromSeconds(Math.Max(0, run.DurationSeconds - TimeBest)))));
        if (User is not null && double.IsFinite(DpsAverage) && DpsAverage > 0)
        {
            double diff = (User.Dps / DpsAverage - 1) * 100;
            parts.Add(Loc.T(diff >= 0 ? "now.verdict_dps_up" : "now.verdict_dps_down", Math.Abs(diff).ToString("0", Loc.Culture)));
        }
        Verdict = parts.Count > 0 ? string.Join(Loc.T("now.verdict_join"), parts) : Loc.T("now.verdict_first");
    }

    public bool HasHistory => User is not null && DpsHistory.Count > 0;

    /// <summary>„Alle 3 Bosse gelegt.“ / „2 von 3 Bossen gelegt.“ / leer</summary>
    public string BossText => BossCount == 0 ? ""
        : BossesKilled == BossCount ? (BossCount == 1 ? Loc.T("now.boss_one") : Loc.T("now.bosses_all", BossCount))
        : Loc.T("now.bosses_some", BossesKilled, BossCount);

    public string RankText => Rank > 0 && PartySize > 1 ? Loc.T("now.rank", Rank, PartySize) : "";

    public static List<RunBoss> Significant(ArchivedRun run) => run.Bosses.Where(b => b.IsSignificant(run.TotalDamage)).ToList();

    public static IEnumerable<BossLine> BossLines(ArchivedRun run) => Significant(run).Select(b =>
    {
        var notes = new List<string>();
        if (b.Attempts > 1) notes.Add(Loc.T("runs.attempts", b.Attempts));
        if (b.Coverage is double c && c < 99.5) notes.Add(Loc.T("fights.coverage", Math.Min(100, c).ToString("0", Loc.Culture)));
        return new BossLine(b.Name, Loc.T("now.boss_from", RunArchive.Fmt(b.StartOffset)), Dur(b.Duration),
            Format.CompactFixed(b.Damage / Math.Max(1, b.Duration.TotalSeconds)) + "/s", b.Killed, string.Join(Loc.T("now.verdict_join"), notes));
    });

    /// <summary>Dauer mit Einheit, damit „18:18“ nicht wie eine Uhrzeit aussieht.</summary>
    public static string Dur(TimeSpan t) => Loc.T(t.TotalHours >= 1 ? "now.dur_hours" : "now.dur_minutes", RunArchive.Fmt(t));

    /// <summary>„Heute um 15:45 Uhr“, „Gestern um …“, sonst Datum.</summary>
    public static string When(DateTime at) => Loc.T("now.when", Day(at), at.ToString("HH:mm"));

    public static string Day(DateTime at) =>
        at.Date == DateTime.Today ? Loc.T("now.today") : at.Date == DateTime.Today.AddDays(-1) ? Loc.T("now.yesterday") : at.ToString("d", Loc.Culture);
}
