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
