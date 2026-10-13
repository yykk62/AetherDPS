using Aion2Meter.Engine;
using Aion2Meter.Engine.Capture;
using Aion2Meter.Engine.Testing;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Aion2Meter.Tests;

/// <summary>
/// Prüft die komplette Kette Rohdaten → Paket → Schaden → Statistik mit
/// künstlichen Paketen. Bricht ein Upstream-Merge das Format, wird es hier rot.
/// </summary>
public sealed class PipelineTests : IDisposable
{
    private const int Mob = 5001;
    private const int Gladiator = 101;
    private const int Cleric = 102;
    private const int GladiatorSkill = 11000001;
    private const int ClericSkill = 17000001;

    private readonly string dataDir = Path.Combine(Path.GetTempPath(), "aion2meter-tests", Guid.NewGuid().ToString("N"));
    private readonly string originalDir = Environment.CurrentDirectory;

    private MeterHost NewHost(string? replay = null) =>
        new(new MeterOptions { DataDir = dataDir, ReplayFile = replay, ReplaySpeed = 0 });

    private static long TotalOf(MeterHost host, int playerId) =>
        host.Sessions.PlayerStats.Where(p => p.PlayerId == playerId).Sum(p => p.TotalDamage);

    [Fact]
    public void Einzelne_Schadenspakete_werden_gezaehlt()
    {
        using var host = NewHost();
        long t = 1_000;
        host.Feed(SyntheticPackets.Heartbeat(), t);
        host.Feed(SyntheticPackets.Damage(Mob, Gladiator, GladiatorSkill, 12_345), t += 100);
        host.Feed(SyntheticPackets.Damage(Mob, Gladiator, GladiatorSkill, 20_000, critical: true), t += 100);
        host.Feed(SyntheticPackets.Damage(Mob, Cleric, ClericSkill, 5_000), t += 100);

        Assert.Equal(32_345, TotalOf(host, Gladiator));
        Assert.Equal(5_000, TotalOf(host, Cleric));
    }

    [Fact]
    public void Zerstueckelte_TCP_Segmente_werden_zusammengesetzt()
    {
        using var host = NewHost();
        var stream = SyntheticPackets.Heartbeat()
            .Concat(SyntheticPackets.Damage(Mob, Gladiator, GladiatorSkill, 1_000))
            .Concat(SyntheticPackets.Damage(Mob, Gladiator, GladiatorSkill, 2_000))
            .Concat(SyntheticPackets.Damage(Mob, Gladiator, GladiatorSkill, 300_000))
            .ToArray();

        // in 7-Byte-Häppchen füttern – Pakete reißen mitten im Feld ab
        for (int i = 0; i < stream.Length; i += 7)
            host.Feed(stream[i..Math.Min(i + 7, stream.Length)], 1_000 + i);

        Assert.Equal(303_000, TotalOf(host, Gladiator));
    }

    [Fact]
    public void LZ4_Container_wird_entpackt()
    {
        using var host = NewHost();
        host.Feed(SyntheticPackets.Heartbeat(), 1_000);
        host.Feed(SyntheticPackets.Compressed(
            SyntheticPackets.Damage(Mob, Gladiator, GladiatorSkill, 7_000),
            SyntheticPackets.Damage(Mob, Cleric, ClericSkill, 3_000),
            SyntheticPackets.Damage(Mob, Gladiator, GladiatorSkill, 8_000)), 1_100);

        Assert.Equal(15_000, TotalOf(host, Gladiator));
        Assert.Equal(3_000, TotalOf(host, Cleric));
    }

    [Fact]
    public void Problem_Mitschnitt_laesst_sich_wieder_abspielen()
    {
        string file;
        using (var live = NewHost())
        {
            live.Feed(SyntheticPackets.Heartbeat(), 1_000);
            live.Feed(SyntheticPackets.Damage(Mob, Gladiator, GladiatorSkill, 4_200), 1_100);
            live.Feed(SyntheticPackets.Damage(Mob, Cleric, ClericSkill, 800), 1_200);
            file = live.Recorder.SaveSnapshot(dataDir, "Test", "test")!;
            Assert.NotNull(file);
        }

        using var replay = NewHost(file);
        ((ReplayCaptureDevice)replay.Capture).PlayToEnd();

        Assert.Equal(4_200, TotalOf(replay, Gladiator));
        Assert.Equal(800, TotalOf(replay, Cleric));
    }

    [Fact]
    public void Bereitschaft_zeichnet_keinen_Schaden_auf()
    {
        using var host = NewHost();
        var mode = host.Mode;   // Zugriff schaltet den Filter ein, Start = Bereitschaft
        host.Feed(SyntheticPackets.Heartbeat(), 1_000);
        host.Feed(SyntheticPackets.Damage(Mob, Gladiator, GladiatorSkill, 50_000), 1_100);
        Assert.Equal(0, TotalOf(host, Gladiator));

        mode.Activate();
        host.Feed(SyntheticPackets.Damage(Mob, Gladiator, GladiatorSkill, 1_500), 1_200);
        Assert.Equal(1_500, TotalOf(host, Gladiator));

        mode.Standby();
        host.Feed(SyntheticPackets.Damage(Mob, Gladiator, GladiatorSkill, 9_999), 1_300);
        mode.Activate();   // frisch ab jetzt – nichts aus der Bereitschaft
        Assert.Equal(0, TotalOf(host, Gladiator));
    }

    [Fact]
    public void Lauf_zaehlt_alle_Gegner_bis_zum_Stopp()
    {
        using var host = NewHost();
        var mode = host.Mode;
        host.Feed(SyntheticPackets.Heartbeat(), 1_000);
        mode.Activate();

        // Dungeon: drei verschiedene Mobs nacheinander
        host.Feed(SyntheticPackets.Damage(5001, Gladiator, GladiatorSkill, 10_000), 1_100);
        host.Feed(SyntheticPackets.Damage(5002, Gladiator, GladiatorSkill, 20_000), 1_200);
        host.Feed(SyntheticPackets.Damage(5003, Cleric, ClericSkill, 4_000), 1_300);
        host.Feed(SyntheticPackets.Damage(5003, Gladiator, GladiatorSkill, 30_000), 1_400);

        var run = host.Run.Snapshot();
        Assert.Equal(60_000, run.Players.Single(p => p.PlayerId == Gladiator).Damage);
        Assert.Equal(4_000, run.Players.Single(p => p.PlayerId == Cleric).Damage);
        Assert.Equal(64_000, run.TotalDamage);
        // Die Zielansicht (Upstream) kennt nur den aktuellen Gegner
        Assert.True(TotalOf(host, Gladiator) < 60_000);

        mode.Standby();
        host.Feed(SyntheticPackets.Damage(5004, Gladiator, GladiatorSkill, 99_999), 1_500);
        Assert.Equal(64_000, host.Run.Snapshot().TotalDamage);   // nach dem Stopp eingefroren
    }

    [Fact]
    public void Gruppenfilter_zeigt_nur_Gruppe_und_im_Zweifel_alle()
    {
        using var host = NewHost();
        var roster = host.Get<PartyRoster>();
        var entities = host.Get<AionDpsMeter.Services.Services.Entity.EntityTracker>();

        // Solo / Gruppe unbekannt: kein Filter
        Assert.True(roster.IsVisible("Fremder", isUser: false, isIdentified: true));

        entities.RaisePartyUpdated([new AionDpsMeter.Core.Models.Player { Name = "Spielerin A" }, new AionDpsMeter.Core.Models.Player { Name = "Spieler B" }]);
        Assert.True(roster.IsVisible("Spieler B", false, true));
        Assert.False(roster.IsVisible("Fremder", false, true));
        Assert.True(roster.IsVisible("Ich", isUser: true, isIdentified: true));           // eigener Charakter immer
        Assert.True(roster.IsVisible("Player_77", isUser: false, isIdentified: false));   // Name noch unbekannt → zeigen
    }

    [Fact]
    public void Verbindungsmonitor_erkennt_Lag_Spitzen()
    {
        using var host = NewHost();
        var monitor = host.Get<ConnectionMonitor>();
        var t0 = DateTime.Now.AddSeconds(-40);
        for (int i = 0; i < 20; i++)
            monitor.Add(new PingSample(t0.AddSeconds(i * 2), 9 + i % 3));
        monitor.Add(new PingSample(t0.AddSeconds(41), 160));
        monitor.Add(new PingSample(t0.AddSeconds(42), 220));

        var stats = monitor.Stats();
        Assert.Equal(220, stats.Current);
        Assert.Equal(2, stats.Spikes.Count);
        Assert.InRange(stats.SpikeThreshold, 40, 100);
    }

    [Fact]
    public void Beschwoerungen_werden_dem_Besitzer_zugeschlagen()
    {
        // Echte Werte aus einem Dungeon-Bosskampf (13.10.), Namen anonymisiert:
        // 3 erkannte Spieler + 18 namenlose Beschwörer-/Magier-Einheiten (Geister/Elementare).
        var lines = new List<MeterLine>
        {
            new(8752, "Kantor", false, true, 18, "Chanter", 582_719, 0, 0),
            new(13032, "Magier", false, true, 15, "Sorcerer", 295_715, 0, 0),
            new(12996, "Beschwoerer", false, true, 16, "Elementalist", 219_727, 0, 0),
        };
        long[] elementals = [25_466, 13_460, 13_025, 10_533, 7_188, 6_887, 4_552, 3_966, 3_687, 3_457, 2_760, 2_302];
        long[] familiars = [8_252, 7_355, 6_784, 6_673, 6_374, 5_723];
        int id = 30_000;
        foreach (var d in elementals) lines.Add(new(id++, $"Player_{id}", false, false, 16, "Elementalist", d, 0, 0));
        foreach (var d in familiars) lines.Add(new(id++, $"Player_{id}", false, false, 15, "Sorcerer", d, 0, 0));

        var result = SummonMerger.Merge(lines);

        Assert.Equal(3, result.Lines.Count);
        Assert.Equal(219_727 + elementals.Sum(), result.Lines.Single(l => l.PlayerId == 12996).TotalDamage);
        Assert.Equal(295_715 + familiars.Sum(), result.Lines.Single(l => l.PlayerId == 13032).TotalDamage);
        Assert.Equal(100, result.Lines.Sum(l => l.DamagePercentage), 3);
        Assert.Equal(12, result.MergedInto[12996].Count);
    }

    [Fact]
    public void Namenloser_ohne_eindeutigen_Besitzer_bleibt_oder_fliegt_im_Gruppenmodus()
    {
        var lines = new List<MeterLine>
        {
            new(1, "Gladi", false, true, 11, "Gladiator", 1000, 0, 0),
            new(2, "Player_2", false, false, 14, "Ranger", 500, 0, 0),   // kein Waldläufer bekannt
        };
        Assert.Equal(2, SummonMerger.Merge(lines).Lines.Count);
        Assert.Single(SummonMerger.Merge(lines, dropUnmerged: true).Lines);
    }

    [Fact]
    public void Deutsche_Namen_sind_geladen()
    {
        using var host = NewHost();
        Assert.True(GermanGameData.PatchedSkillCount > 1_000, $"nur {GermanGameData.PatchedSkillCount} Skills übersetzt");
        Assert.Equal("Sandsack", GermanGameData.MobName(new AionDpsMeter.Core.Models.Mob { MobCode = 2000000 }));
        Assert.Equal("Waldläufer", GermanGameData.ClassName(14, "Ranger"));
    }

    [Fact]
    public void Echte_Mitschnitte_laufen_fehlerfrei_durch()
    {
        // Mitschnitte aus "Problem melden" landen in Captures/. Jeder muss sich
        // abspielen lassen und Schaden liefern – das ist die Regressionsbasis.
        var dir = Path.Combine(AppContext.BaseDirectory, "Captures");
        if (!Directory.Exists(dir)) return;
        foreach (var file in Directory.GetFiles(dir, "*.txt"))
        {
            using var host = NewHost(file);
            ((ReplayCaptureDevice)host.Capture).PlayToEnd();
            Assert.True(host.Sessions.PlayerStats.Sum(p => p.TotalDamage) > 0, $"kein Schaden in {Path.GetFileName(file)}");
        }
    }

    public void Dispose()
    {
        Environment.CurrentDirectory = originalDir;
        try { Directory.Delete(dataDir, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
