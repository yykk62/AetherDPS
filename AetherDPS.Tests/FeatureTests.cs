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

        var text = RunArchive.ToDiscordText(run, (id, fallback) => GameText.ClassName(id, fallback));
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
    public void Alte_Zeitplaene_werden_zu_festen_Terminen()
    {
        Directory.CreateDirectory(dataDir);
        // Stand 0.5.0: Riss mit eigener Zeit (aus, ohne Erinnerung) + geschätzte Vorlage
        File.WriteAllText(Path.Combine(dataDir, "timer.json"), """
            { "Schedules": [
                { "Name": "Raumzeit-Riss", "Category": "Riss", "Times": ["01:30"], "IntervalHours": 2, "Enabled": false, "Reminder": false },
                { "Name": "Riss-Eroberung (PvP)", "Category": "Riss", "Times": ["20:00"], "Unconfirmed": true, "Enabled": false } ] }
            """);
        using var host = NewHost();
        var schedules = host.Get<TimerService>().Schedules;
        Assert.Equal(["rift", "artifact_siege"], schedules.Select(s => s.Id));
        var rift = schedules[0];
        Assert.Equal("Raumzeit-Riss", rift.DisplayName);
        Assert.Equal(["02:00"], rift.Times);   // feste EU-Zeit statt eigener
        Assert.Equal(3, rift.IntervalHours);
        Assert.False(rift.Enabled);            // An/aus bleibt erhalten
        Assert.False(rift.Reminder);
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

    [Fact]
    public void Pills_werden_nach_ihrer_Lage_gruppiert()
    {
        const double w = 150, h = 46;
        // nebeneinander (leicht schief) → eine Zeile
        var row = PillLayout.Infer([("A", 100, 50), ("B", 260, 58), ("C", 410, 45)], w, h);
        Assert.Equal([(0, 0), (1, 0), (2, 0)], row.Select(c => (c.Col, c.Row)));
        // untereinander → eine Spalte
        var col = PillLayout.Infer([("A", 100, 50), ("B", 108, 102), ("C", 95, 150)], w, h);
        Assert.Equal([(0, 0), (0, 1), (0, 2)], col.Select(c => (c.Col, c.Row)));
        // 2 oben, 2 unten → 2×2
        var block = PillLayout.Infer([("A", 100, 50), ("B", 255, 52), ("C", 98, 101), ("D", 260, 99)], w, h);
        Assert.Equal(["A:0,0", "B:1,0", "C:0,1", "D:1,1"], block.Select(c => $"{c.Name}:{c.Col},{c.Row}"));
        // fast übereinander → keine doppelte Zelle
        var stacked = PillLayout.Infer([("A", 100, 50), ("B", 110, 55)], w, h);
        Assert.Equal(2, stacked.Select(c => (c.Col, c.Row)).Distinct().Count());
        // feste Spaltenzahl
        Assert.Equal([(0, 0), (1, 0), (0, 1)], PillLayout.Grid(["A", "B", "C"], 2).Select(c => (c.Col, c.Row)));
    }

    [Fact]
    public void Pet_Stufen_und_eigene_Position_aus_dem_Spielverkehr()
    {
        using var host = NewHost();
        var pets = host.Get<Pets>();
        var entities = host.Get<AionDpsMeter.Services.Services.Entity.EntityTracker>();
        Assert.True(pets.All.Count > 150);
        Assert.Equal(3, pets.Maps.Count);

        // Katalog: Topfkrabbe (1007) spawnt in Verteron und Altgard, das Monster führt zurück zum Pet
        var crab = pets.Get(1007)!;
        Assert.Equal(2, pets.Maps.Count(m => pets.Spawns(crab, m).Count > 0));
        var spawns = pets.Maps.SelectMany(m => pets.Spawns(crab, m)).ToList();
        Assert.Equal(1007, pets.PetOfNpc(spawns[0].Npc));
        Assert.False(pets.HasSpawns(pets.Get(1077)!));   // Harcon (Feldboss) hat keine Pet-Spawns

        host.Feed(SyntheticPackets.Heartbeat(), 1_000);
        entities.SetSessionPlayerName(4613, "Testheld", 0, "#1317", isUser: true);
        Assert.Null(pets.Level(1077));   // Liste noch nie gesehen
        host.Feed(SyntheticPackets.PetList(1077, (1077, 1, 12), (1003, 3, 0)), 1_100);
        Assert.Equal(1, pets.Level(1077));
        Assert.Equal(12, pets.Progress(1077));
        Assert.Equal(3, pets.Level(1003));
        Assert.Equal(0, pets.Level(1004));   // nicht in der Liste = noch nicht gesammelt

        host.Feed(SyntheticPackets.MapLoad(101001), 1_200);
        host.Feed(SyntheticPackets.Position(9999, 1, 2), 1_300);      // jemand anderes
        Assert.Null(pets.Position);
        host.Feed(SyntheticPackets.Position(4613, 81_840, -20_010), 1_400);
        Assert.Equal(81_840, pets.Position!.Value.X, 0);
        Assert.Equal("verteron", pets.CurrentMap!.Key);   // Ebene 101001 gehört zu Verteron 1010

        // Nächste Pets: Entfernung in Metern, Richtung
        var near = pets.Nearest(pets.CurrentMap!, 81_840, -20_010, _ => true, 3);
        Assert.Equal(3, near.Count);
        Assert.True(near[0].Meters <= near[1].Meters);
        Assert.InRange(near[0].Bearing, 0, 360);

        // Spielkarte offen = Feldboss-Liste kommt
        Assert.Null(pets.MapOpenSeen);
        host.Feed(SyntheticPackets.FieldBossList(1010), 1_500);
        Assert.NotNull(pets.MapOpenSeen);

        // überlebt einen Neustart
        using var again = NewHost();
        Assert.Equal(1, again.Get<Pets>().Level(1077));
    }

    public void Dispose()
    {
        Environment.CurrentDirectory = originalDir;
        try { Directory.Delete(dataDir, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
