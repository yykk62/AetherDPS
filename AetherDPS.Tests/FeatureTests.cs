using AetherDPS.Engine;
using AetherDPS.Engine.Testing;

namespace AetherDPS.Tests;

/// <summary>Archiv, Timer, Checkliste, Buff-Laufzeit.</summary>
public sealed class FeatureTests : IDisposable
{
    private readonly string dataDir = Path.Combine(Path.GetTempPath(), "aetherdps-tests", Guid.NewGuid().ToString("N"));
    private readonly string originalDir = Environment.CurrentDirectory;

    private MeterHost NewHost() => new(new MeterOptions { DataDir = dataDir });

    [Fact]
    public void Lauf_wird_beim_Stoppen_archiviert()
    {
        using var host = NewHost();
        var archive = host.Get<RunArchive>();
        host.Get<MeterSettings>().PartyOnly = false;   // Testspieler sind keine „du“-Charaktere
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
    public void Charakter_wird_ohne_Teleport_wiedererkannt_und_Buffs_nachgetragen()
    {
        // gespeicherter Charakter aus einer früheren Sitzung
        Directory.CreateDirectory(dataDir);
        File.WriteAllText(Path.Combine(dataDir, "aetherdps.json"),
            """{ "CharacterName": "Testheld", "CharacterServer": "EU-1", "CharacterClassId": 11 }""");

        using var host = NewHost();
        var live = host.Get<LiveTimers>();
        host.Mode.Activate();
        host.Feed(SyntheticPackets.Heartbeat(), 1_000);

        // Buff auf 101 – Upstream weiß noch nicht, dass 101 du bist
        host.Sessions.ProcessBuffEvent(new AionDpsMeter.Core.Models.BuffEvent
        {
            EntityId = 101, BuffId = 11000100, BuffName = "Kampfrausch", DurationMs = 20_000, AppliedAt = DateTime.Now, CasterId = 101,
        });
        Assert.Empty(live.Active());

        // Abklingzeit für Skill X kommt beim Auslösen (VOR dem Treffer), dann trifft 101 mit Skill X → Stimme 1;
        // danach umgekehrte Reihenfolge → Stimme 2 → 101 bist du
        var skill = AionDpsMeter.Core.GameData.Services.GameDataProvider.Instance.GetSkillOrDefault(11000001);
        void Cooldown() => host.Sessions.RegisterSkillCdEvent(new AionDpsMeter.Services.Services.Timed.TimedEvent
        {
            Id = (uint)skill.Id, Name = skill.Name, IconUrl = "", Duration = TimeSpan.FromSeconds(8),
        });
        Cooldown();
        host.Feed(SyntheticPackets.Damage(5001, 101, 11000001, 1_000), 1_100);
        Assert.Empty(live.Active());   // eine Stimme reicht noch nicht
        host.Feed(SyntheticPackets.Damage(5001, 101, 11000001, 1_000), 1_200);
        Cooldown();

        Assert.Contains(live.Active(), i => i.Name == "Kampfrausch");   // nachgetragen
        var me = host.Get<AionDpsMeter.Services.Services.Entity.EntityTracker>().GetPlayerEntity(101);
        Assert.Equal("Testheld", me?.Name);                            // gespeicherter Name
        Assert.True(me?.IsUser);
    }

    [Fact]
    public void Solo_ohne_erkannten_Charakter_zeigt_niemanden()
    {
        using var host = NewHost();
        var settings = host.Get<MeterSettings>();
        settings.PartyOnly = true;
        var strangers = new List<MeterLine>
        {
            new(1, "Fremder", false, true, 11, "Gladiator", 50_000, 0, 0),
            new(2, "Player_2", false, false, 14, "Ranger", 7_000, 0, 0),
        };
        Assert.Empty(MeterFilter.Apply(strangers, host.Get<PartyRoster>(), settings).Lines);
    }

    [Fact]
    public void Gespeicherter_Charakter_wird_nur_in_derselben_Spielsitzung_wiederhergestellt()
    {
        Directory.CreateDirectory(dataDir);
        File.WriteAllText(Path.Combine(dataDir, "aetherdps.json"),
            """{ "CharacterName": "Testheld", "CharacterSessionId": 9669, "CharacterSeenAt": "2026-10-13T18:00:00" }""");
        using var host = NewHost();
        var self = host.Get<SelfIdentity>();
        var entities = host.Get<AionDpsMeter.Services.Services.Entity.EntityTracker>();

        Assert.False(self.Restore(DateTime.Parse("2026-10-13 19:00")));   // Spiel danach neu gestartet
        Assert.False(entities.IsCurrentPlayer(9669));
        Assert.True(self.Restore(DateTime.Parse("2026-10-13 16:20")));    // gleiche Spielsitzung
        Assert.True(entities.IsCurrentPlayer(9669));
        Assert.Equal("Testheld", entities.GetPlayerEntity(9669)?.Name);
    }

    [Fact]
    public void Solo_zeigt_nur_dich_und_deine_Beschwoerungen()
    {
        using var host = NewHost();
        var settings = host.Get<MeterSettings>();
        settings.PartyOnly = true;
        var lines = new List<MeterLine>
        {
            new(1, "Ich", true, true, 16, "Elementalist", 10_000, 0, 0),
            new(2, "Fremder", false, true, 11, "Gladiator", 50_000, 0, 0),
            new(3, "Player_3", false, false, 16, "Elementalist", 2_000, 0, 0),   // mein Geist
            new(4, "Player_4", false, false, 14, "Ranger", 7_000, 0, 0),         // fremder, noch namenlos
        };
        var result = MeterFilter.Apply(lines, host.Get<PartyRoster>(), settings);
        var only = Assert.Single(result.Lines);
        Assert.Equal(12_000, only.TotalDamage);
    }

    [Fact]
    public void Zeitplan_mit_Wochentagen_und_Takt()
    {
        var tue = DateTime.Parse("2026-10-13 21:00");   // Dienstag
        var weekly = new ScheduleEntry { Days = [DayOfWeek.Monday, DayOfWeek.Thursday, DayOfWeek.Saturday], Times = ["20:00", "23:00"] };
        Assert.Equal(DateTime.Parse("2026-10-15 20:00"), weekly.Next(tue));          // nächster Do
        Assert.Equal(DateTime.Parse("2026-10-15 23:00"), weekly.Next(DateTime.Parse("2026-10-15 20:30")));

        var daily = new ScheduleEntry { Times = ["18:00"] };
        Assert.Equal(DateTime.Parse("2026-10-14 18:00"), daily.Next(tue));

        var rift = new ScheduleEntry { Times = ["02:00"], IntervalHours = 3 };
        Assert.Equal(DateTime.Parse("2026-10-13 23:00"), rift.Next(tue));
        Assert.Equal("Mo, Do, Sa · 20:00, 23:00", weekly.Describe());
    }

    [Fact]
    public void Alte_Riss_Einstellung_wird_in_Zeitplaene_uebernommen()
    {
        Directory.CreateDirectory(dataDir);
        File.WriteAllText(Path.Combine(dataDir, "timer.json"), """{ "RiftAnchor": "01:30", "RiftIntervalHours": 2 }""");
        using var host = NewHost();
        var rift = host.Get<TimerService>().Schedules.First();
        Assert.Equal("Raumzeit-Riss", rift.Name);
        Assert.Equal(["01:30"], rift.Times);
        Assert.Equal(2, rift.IntervalHours);
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
