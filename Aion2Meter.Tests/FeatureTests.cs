using Aion2Meter.Engine;
using Aion2Meter.Engine.Testing;

namespace Aion2Meter.Tests;

/// <summary>Archiv, Timer, Checkliste, Buff-Laufzeit.</summary>
public sealed class FeatureTests : IDisposable
{
    private readonly string dataDir = Path.Combine(Path.GetTempPath(), "aion2meter-tests", Guid.NewGuid().ToString("N"));
    private readonly string originalDir = Environment.CurrentDirectory;

    private MeterHost NewHost() => new(new MeterOptions { DataDir = dataDir });

    [Fact]
    public void Lauf_wird_beim_Stoppen_archiviert()
    {
        using var host = NewHost();
        var archive = host.Get<RunArchive>();
        var mode = host.Mode;
        host.Feed(SyntheticPackets.Heartbeat(), 1_000);
        mode.Activate();
        host.Feed(SyntheticPackets.Damage(5001, 101, 11000001, 40_000), 1_100);
        host.Feed(SyntheticPackets.Damage(5001, 102, 17000001, 10_000), 1_200);
        mode.Standby();

        var run = Assert.Single(archive.All());
        Assert.Equal(50_000, run.TotalDamage);
        Assert.Equal(2, run.Players.Count);
        Assert.True(File.Exists(Path.Combine(dataDir, "Laeufe", $"{run.Id}.json")));

        // neu laden (frische Instanz) liest die Datei wieder ein
        using var again = NewHost();
        Assert.Equal(50_000, Assert.Single(again.Get<RunArchive>().All()).TotalDamage);

        var text = RunArchive.ToDiscordText(run, (id, fallback) => GermanGameData.ClassName(id, fallback));
        Assert.Contains("Gladiator", text);
        Assert.Contains("```", text);
    }

    [Fact]
    public void Leerer_Lauf_wird_nicht_archiviert()
    {
        using var host = NewHost();
        host.Mode.Activate();
        host.Mode.Standby();
        Assert.Empty(host.Get<RunArchive>().All());
    }

    [Theory]
    [InlineData("2026-10-13 18:10", "2026-10-13 20:00")]
    [InlineData("2026-10-13 23:30", "2026-10-14 02:00")]
    [InlineData("2026-10-13 01:59", "2026-10-13 02:00")]
    [InlineData("2026-10-13 02:00", "2026-10-13 05:00")]
    public void Riss_Zeitplan_alle_3_Stunden(string now, string expected)
    {
        Assert.Equal(DateTime.Parse(expected), TimerService.NextRift(DateTime.Parse(now), "02:00", 3));
    }

    [Fact]
    public void Checkliste_setzt_nach_Reset_zurueck()
    {
        // Mittwoch 13.10.2026? – Datum egal, Logik: Reset 6 Uhr
        Assert.Equal(DateTime.Parse("2026-10-13 06:00"), ChecklistService.LastDailyReset(DateTime.Parse("2026-10-13 18:00"), 6));
        Assert.Equal(DateTime.Parse("2026-10-12 06:00"), ChecklistService.LastDailyReset(DateTime.Parse("2026-10-13 05:59"), 6));

        var wed = ChecklistService.LastWeeklyReset(DateTime.Parse("2026-10-13 18:00"), DayOfWeek.Wednesday, 6);
        Assert.Equal(DayOfWeek.Wednesday, wed.DayOfWeek);
        Assert.True(wed <= DateTime.Parse("2026-10-13 18:00") && wed > DateTime.Parse("2026-10-06 18:00"));

        using var host = NewHost();
        var list = host.Get<ChecklistService>();
        var item = list.Data.Items[0];
        item.DoneAt = DateTime.Parse("2026-10-13 07:00");
        Assert.True(list.IsDone(item, DateTime.Parse("2026-10-13 20:00")));
        Assert.False(list.IsDone(item, DateTime.Parse("2026-10-14 06:30")));   // nach dem nächsten Reset
    }

    [Fact]
    public void Buff_Laufzeit_zaehlt_nur_Kampfphasen_und_keine_Doppelungen()
    {
        var t = DateTime.Parse("2026-10-13 18:00:00");
        var combat = new List<(DateTime, DateTime)> { (t, t.AddSeconds(60)) };
        // zwei überlappende Anwendungen (0–30 s, 20–40 s) + eine außerhalb des Kampfes
        var buffs = new List<(DateTime, DateTime)>
        {
            (t, t.AddSeconds(30)), (t.AddSeconds(20), t.AddSeconds(40)), (t.AddSeconds(100), t.AddSeconds(130)),
        };
        Assert.Equal(40, RunTracker.Overlap(buffs, combat), 3);
    }

    public void Dispose()
    {
        Environment.CurrentDirectory = originalDir;
        try { Directory.Delete(dataDir, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
