using System.IO.Pipes;
using System.Text.Json.Nodes;
using AetherDPS.Engine;
using AetherDPS.Engine.Community;
using AetherDPS.Engine.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace AetherDPS.Tests;

/// <summary>Discord Rich Presence: Texte, Zustand aus den Paketen, lokales IPC-Protokoll.</summary>
public sealed class PresenceTests : IDisposable
{
    private readonly string dataDir = Path.Combine(Path.GetTempPath(), "aetherdps-tests", Guid.NewGuid().ToString("N"));
    private readonly string originalDir = Environment.CurrentDirectory;
    private static readonly DateTime Since = new(2026, 10, 16, 20, 0, 0);

    public PresenceTests() => Loc.Load("de");

    [Fact]
    public void Text_fuer_jeden_Zustand()
    {
        var boss = PresenceText.Build(new PresenceInfo(PresenceKind.BossFight, Since, "Draupnir", "Ruthilis des Schmerzes"), showNames: true);
        Assert.Equal("AION 2 · Bosskampf: Ruthilis des Schmerzes", boss.Details);
        Assert.Equal("Dungeon: Draupnir", boss.State);
        Assert.Equal(Since, boss.Since);
        Assert.Equal("AetherDPS herunterladen", boss.DownloadLabel);
        Assert.Equal("Discord beitreten", boss.DiscordLabel);

        var dungeon = PresenceText.Build(new PresenceInfo(PresenceKind.Dungeon, Since, "Draupnir"), showNames: true);
        Assert.Equal("AION 2 · Dungeon: Draupnir", dungeon.Details);
        Assert.Equal("Kostenloser DPS-Meter", dungeon.State);

        Assert.Equal("AION 2 · In Bereitschaft", PresenceText.Build(new PresenceInfo(PresenceKind.Standby, Since), true).Details);
        Assert.Equal("AION 2 · Offene Welt", PresenceText.Build(new PresenceInfo(PresenceKind.OpenWorld, Since), true).Details);
        Assert.Equal("AION 2 · Wartet auf das Spiel", PresenceText.Build(new PresenceInfo(PresenceKind.WaitingForGame, Since), true).Details);
        // Boss in der offenen Welt: keine Dungeon-Zeile
        Assert.Equal("Kostenloser DPS-Meter", PresenceText.Build(new PresenceInfo(PresenceKind.BossFight, Since, null, "Harcon"), true).State);
        // Unbekannter Dungeon (Name fehlt in den Spieldaten)
        Assert.Equal("AION 2 · Im Dungeon", PresenceText.Build(new PresenceInfo(PresenceKind.Dungeon, Since, null), true).Details);
    }

    [Fact]
    public void Namen_lassen_sich_ausblenden()
    {
        var boss = PresenceText.Build(new PresenceInfo(PresenceKind.BossFight, Since, "Draupnir", "Ruthilis des Schmerzes"), showNames: false);
        Assert.Equal("AION 2 · Bosskampf", boss.Details);
        Assert.Equal("Im Dungeon", boss.State);
        Assert.Equal("AION 2 · Im Dungeon", PresenceText.Build(new PresenceInfo(PresenceKind.Dungeon, Since, "Draupnir"), false).Details);
        Assert.DoesNotContain("Draupnir", System.Text.Json.JsonSerializer.Serialize(boss));
    }

    [Fact]
    public void Lange_Texte_werden_gekuerzt_und_Knoepfe_passen_in_jeder_Sprache()
    {
        var a = PresenceText.Build(new PresenceInfo(PresenceKind.BossFight, Since, new string('D', 300), new string('B', 300)), true);
        Assert.Equal(PresenceText.MaxText, a.Details.Length);
        Assert.EndsWith("…", a.Details);
        Assert.True(a.State!.Length <= PresenceText.MaxText);
        try
        {
            foreach (var lang in Loc.Available)
            {
                Loc.Load(lang);
                var b = PresenceText.Build(new PresenceInfo(PresenceKind.Standby, Since), true);
                Assert.InRange(b.DownloadLabel.Length, 2, PresenceText.MaxButton);
                Assert.InRange(b.DiscordLabel.Length, 2, PresenceText.MaxButton);
                Assert.DoesNotContain("presence.", b.Details + b.State + b.LargeText);
            }
        }
        finally { Loc.Load("de"); }
    }

    [Fact]
    public void Aktivitaet_fuer_Discord()
    {
        var json = DiscordPresence.BuildActivity(PresenceText.Build(new PresenceInfo(PresenceKind.Dungeon, Since, "Draupnir"), true));
        Assert.Equal("AION 2 · Dungeon: Draupnir", json["details"]!.GetValue<string>());
        Assert.Equal(new DateTimeOffset(Since).ToUnixTimeSeconds(), json["timestamps"]!["start"]!.GetValue<long>());
        Assert.Equal("logo", json["assets"]!["large_image"]!.GetValue<string>());
        var buttons = json["buttons"]!.AsArray();
        Assert.Equal(2, buttons.Count);
        Assert.Equal("https://github.com/yykk62/AetherDPS/releases/latest", buttons[0]!["url"]!.GetValue<string>());
        Assert.Equal("https://discord.gg/xExgsyDEP2", buttons[1]!["url"]!.GetValue<string>());
    }

    [Fact]
    public void Zustand_aus_Dungeon_und_Bosskampf()
    {
        const int Boss = 7001, Ruthilis = 2300708;
        using var host = new MeterHost(new MeterOptions { DataDir = dataDir });
        host.Get<MeterSettings>().PartyOnly = false;
        var tracker = host.Get<PresenceTracker>();
        long t = 1_000;
        host.Feed(SyntheticPackets.Heartbeat(), t);
        Assert.Equal(PresenceKind.Standby, tracker.Current(DateTime.Now).Kind);
        tracker.GameRunning = () => false;
        Assert.Equal(PresenceKind.WaitingForGame, tracker.Current(DateTime.Now).Kind);
        tracker.GameRunning = () => true;

        host.Feed(SyntheticPackets.MapLoad(600032, 2), t += 100);
        var info = tracker.Current(DateTime.Now);
        Assert.Equal(PresenceKind.Dungeon, info.Kind);
        Assert.Equal("Draupnir", info.Dungeon);

        // In Bereitschaft wird kein Kampf erfasst → noch kein Bosskampf
        host.Feed(SyntheticPackets.MobInfo(Boss, Ruthilis, 10_000_000), t += 100);
        host.Feed(SyntheticPackets.Damage(Boss, 101, 11000001, 50_000), t += 100);
        Assert.Equal(PresenceKind.Dungeon, tracker.Current(DateTime.Now).Kind);

        host.Mode.Activate();
        host.Feed(SyntheticPackets.Damage(5001, 101, 11000001, 1_000), t += 100);   // normaler Gegner
        Assert.Equal(PresenceKind.Dungeon, tracker.Current(DateTime.Now).Kind);
        host.Feed(SyntheticPackets.Damage(Boss, 101, 11000001, 50_000), t += 100);
        info = tracker.Current(DateTime.Now);
        Assert.Equal(PresenceKind.BossFight, info.Kind);
        Assert.Equal("Ruthilis des Schmerzes", info.Boss);
        Assert.Equal("Draupnir", info.Dungeon);

        // 30 s ohne Treffer → wieder Dungeon
        Assert.Equal(PresenceKind.Dungeon, tracker.Current(DateTime.Now + PresenceTracker.BossTimeout + TimeSpan.FromSeconds(1)).Kind);

        host.Feed(SyntheticPackets.MapLoad(1010, 3), t += 100);
        Assert.Equal(PresenceKind.BossFight, tracker.Current(DateTime.Now).Kind);   // Boss gerade noch getroffen
        Assert.Null(tracker.Current(DateTime.Now).Dungeon);
        Assert.Equal(PresenceKind.OpenWorld, tracker.Current(DateTime.Now + TimeSpan.FromMinutes(1)).Kind);
        host.Mode.Standby();
        Assert.Equal(PresenceKind.Standby, tracker.Current(DateTime.Now + TimeSpan.FromMinutes(1)).Kind);
    }

    [Fact]
    public async Task Spricht_das_IPC_Protokoll_und_haelt_das_Rate_Limit_ein()
    {
        var prefix = $"aetherdps-test-ipc-{Guid.NewGuid():N}-";
        using var server = new NamedPipeServerStream(prefix + "0", PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        var frames = new List<(int Op, JsonNode? Json)>();
        var serverTask = Task.Run(() =>
        {
            server.WaitForConnection();
            var hello = DiscordIpc.ReadFrame(server, CancellationToken.None);
            lock (frames) frames.Add(hello);
            server.Write(DiscordIpc.Frame(DiscordIpc.OpFrame, """{"cmd":"DISPATCH","evt":"READY","data":{"v":1}}"""));
            try
            {
                while (true)
                {
                    var f = DiscordIpc.ReadFrame(server, CancellationToken.None);
                    lock (frames) frames.Add(f);
                    server.Write(DiscordIpc.Frame(DiscordIpc.OpFrame, """{"cmd":"SET_ACTIVITY","evt":null,"data":{}}"""));
                }
            }
            catch (Exception) { }   // Client hat geschlossen
        });

        using var host = new MeterHost(new MeterOptions { DataDir = dataDir });
        var settings = host.Get<MeterSettings>();
        settings.CharacterName = "Testheld";
        var tracker = host.Get<PresenceTracker>();
        var presence = new DiscordPresence(settings, tracker, NullLogger<DiscordPresence>.Instance) { PipePrefix = prefix };
        var now = DateTime.Now;

        presence.Tick(now);   // aus → keine Verbindung
        Assert.False(presence.Connected);
        settings.DiscordPresence = true;
        presence.Tick(now);
        Assert.True(presence.Connected);
        WaitFor(() => frames.Count >= 2);

        host.Feed(SyntheticPackets.Heartbeat(), 1_000);
        host.Feed(SyntheticPackets.MapLoad(600032, 2), 1_100);
        presence.Tick(now + TimeSpan.FromSeconds(5));     // geändert, aber erst 5 s her → wartet
        presence.Tick(now + TimeSpan.FromSeconds(16));    // jetzt
        WaitFor(() => frames.Count >= 3);
        presence.Tick(now + TimeSpan.FromSeconds(40));    // nichts geändert → nichts senden

        settings.DiscordPresence = false;
        presence.Tick(now + TimeSpan.FromSeconds(41));   // aus → Anzeige entfernen, Verbindung zu
        Assert.False(presence.Connected);
        await serverTask.WaitAsync(TimeSpan.FromSeconds(5));

        List<(int Op, JsonNode? Json)> all;
        lock (frames) all = [.. frames];
        Assert.Equal(DiscordIpc.OpHandshake, all[0].Op);
        Assert.Equal(DiscordPresence.ClientId, all[0].Json!["client_id"]!.GetValue<string>());
        Assert.Equal(1, all[0].Json!["v"]!.GetValue<int>());

        var activities = all.Skip(1).Select(f => f.Json!).ToList();
        Assert.All(activities, a => Assert.Equal("SET_ACTIVITY", a["cmd"]!.GetValue<string>()));
        Assert.Equal(3, activities.Count);
        Assert.Equal("AION 2 · In Bereitschaft", activities[0]["args"]!["activity"]!["details"]!.GetValue<string>());
        Assert.Equal("AION 2 · Dungeon: Draupnir", activities[1]["args"]!["activity"]!["details"]!.GetValue<string>());
        Assert.Null(activities[2]["args"]!["activity"]);   // entfernt
        Assert.Equal(Environment.ProcessId, activities[0]["args"]!["pid"]!.GetValue<int>());
        // Nie Spieler- oder Charakternamen
        Assert.DoesNotContain("Testheld", string.Join("", activities.Select(a => a.ToJsonString())));
    }

    [Fact]
    public async Task Ohne_Discord_still_und_erst_nach_30_s_neu_versuchen()
    {
        using var host = new MeterHost(new MeterOptions { DataDir = dataDir });
        var settings = host.Get<MeterSettings>();
        settings.DiscordPresence = true;
        var prefix = $"aetherdps-test-ipc-{Guid.NewGuid():N}-";
        var presence = new DiscordPresence(settings, host.Get<PresenceTracker>(), NullLogger<DiscordPresence>.Instance) { PipePrefix = prefix };
        var now = DateTime.Now;
        presence.Tick(now);
        Assert.False(presence.Connected);

        // Discord startet – vor Ablauf der 30 s wird nicht neu verbunden, danach schon
        using var server = new NamedPipeServerStream(prefix + "3", PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        var serverTask = Task.Run(() =>
        {
            server.WaitForConnection();
            DiscordIpc.ReadFrame(server, CancellationToken.None);
            server.Write(DiscordIpc.Frame(DiscordIpc.OpFrame, """{"cmd":"DISPATCH","evt":"READY","data":{"v":1}}"""));
            try { while (true) DiscordIpc.ReadFrame(server, CancellationToken.None); } catch (Exception) { }
        });
        presence.Tick(now + TimeSpan.FromSeconds(10));
        Assert.False(presence.Connected);
        presence.Tick(now + DiscordPresence.RetryInterval + TimeSpan.FromSeconds(1));
        Assert.True(presence.Connected);
        presence.Dispose();
        await serverTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    private static void WaitFor(Func<bool> condition)
    {
        for (int i = 0; i < 100 && !condition(); i++) Thread.Sleep(20);
        Assert.True(condition());
    }

    public void Dispose()
    {
        Environment.CurrentDirectory = originalDir;
        try { Directory.Delete(dataDir, true); } catch (IOException) { }
    }
}
