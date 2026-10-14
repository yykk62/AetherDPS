using System.Net;
using System.Net.NetworkInformation;
using AionDpsMeter.Services.PacketCapture;
using AionDpsMeter.Services.Services.Session;

namespace AetherDPS.Engine;

public readonly record struct PingSample(DateTime At, int Ms);

public sealed record ConnectionStats(
    int? Current, double? Average, int? Min, int? Max, double? Jitter,
    IReadOnlyList<PingSample> Spikes, double SpikeThreshold, long Segments, long Retransmits, double RetransmitPercent);

public sealed record ProbeResult(string Target, IPAddress? Address, double? AverageMs, int Lost, int Sent)
{
    public bool Reachable => AverageMs is not null;
    public double LossPercent => Sent > 0 ? Lost * 100.0 / Sent : 0;
}

public enum Verdict { AllesOk, Heimnetz, Anbieter, Spielserver, KeineDaten }

public sealed record Diagnosis(Verdict Verdict, bool IsWifi, ProbeResult Router, ProbeResult Internet, ProbeResult GameServer);

/// <summary>
/// Verbindungs-Monitor: sammelt den Spiel-Ping (aus den Serverzeit-Paketen, läuft passiv
/// mit) und prüft auf Knopfdruck per ICMP, ob ein Problem im Heimnetz, beim Anbieter
/// oder auf dem Weg zum Spielserver liegt. Verbessert die Route nicht – zeigt nur, wo es hakt.
/// </summary>
public sealed class ConnectionMonitor
{
    public static readonly TimeSpan History = TimeSpan.FromMinutes(15);
    private const string InternetReference = "1.1.1.1";

    private readonly IPacketCaptureDevice capture;
    private readonly Lock gate = new();
    private readonly List<PingSample> samples = [];

    public ConnectionMonitor(CombatSessionManager sessions, IPacketCaptureDevice capture)
    {
        this.capture = capture;
        sessions.PingUpdated += (_, ms) => Add(new PingSample(DateTime.Now, ms));
    }

    public void Add(PingSample sample)
    {
        if (sample.Ms is < 0 or > 5000) return;
        lock (gate)
        {
            samples.Add(sample);
            var cutoff = sample.At - History;
            int old = samples.FindIndex(s => s.At >= cutoff);
            if (old > 0) samples.RemoveRange(0, old);
        }
    }

    public IReadOnlyList<PingSample> Samples() { lock (gate) return samples.ToList(); }

    public IPAddress? ServerAddress => (capture as CaptureDevice)?.ServerAddress;

    public ConnectionStats Stats(DateTime? now = null)
    {
        var at = now ?? DateTime.Now;
        List<PingSample> all, lastMinute;
        lock (gate)
        {
            all = samples.ToList();
            lastMinute = samples.Where(s => s.At >= at.AddMinutes(-1)).ToList();
        }

        var window = lastMinute.Count >= 3 ? lastMinute : all;
        double? avg = window.Count > 0 ? window.Average(s => s.Ms) : null;
        double? jitter = window.Count > 1
            ? window.Zip(window.Skip(1), (a, b) => Math.Abs(b.Ms - a.Ms)).Average()
            : null;

        // Spitze = deutlich über dem üblichen Wert (Median über alles, robust gegen Ausreißer)
        var spikes = new List<PingSample>();
        double threshold = double.MaxValue;
        if (all.Count >= 5)
        {
            var sorted = all.Select(s => s.Ms).Order().ToList();
            double median = sorted[sorted.Count / 2];
            threshold = Math.Max(median * 2, median + 40);
            spikes = all.Where(s => s.Ms >= threshold).TakeLast(20).ToList();
        }

        var device = capture as CaptureDevice;
        long segments = device?.TcpSegments ?? 0, retrans = device?.TcpRetransmits ?? 0;
        return new ConnectionStats(
            all.Count > 0 ? all[^1].Ms : null, avg,
            window.Count > 0 ? window.Min(s => s.Ms) : null,
            window.Count > 0 ? window.Max(s => s.Ms) : null,
            jitter, spikes, threshold, segments, retrans,
            segments > 0 ? retrans * 100.0 / segments : 0);
    }

    /// <summary>Router, Internet und Spielserver je 5× anpingen und eine Einschätzung ableiten.</summary>
    public async Task<Diagnosis> DiagnoseAsync(CancellationToken token = default)
    {
        var (gateway, isWifi) = FindGateway();
        var routerTask = ProbeAsync("router", gateway, token);
        var internetTask = ProbeAsync("internet", IPAddress.Parse(InternetReference), token);
        var serverTask = ProbeAsync("server", ServerAddress, token);
        await Task.WhenAll(routerTask, internetTask, serverTask);

        var router = routerTask.Result;
        var internet = internetTask.Result;
        var server = serverTask.Result;
        var stats = Stats();

        Verdict verdict;
        if (!router.Reachable && !internet.Reachable) verdict = Verdict.KeineDaten;
        else if (router.LossPercent > 0 || router.AverageMs > (isWifi ? 15 : 5)) verdict = Verdict.Heimnetz;
        else if (internet.LossPercent > 0 || internet.AverageMs > 60) verdict = Verdict.Anbieter;
        else if (stats.Spikes.Count > 3 || stats.RetransmitPercent > 1 || stats.Average > 100) verdict = Verdict.Spielserver;
        else verdict = Verdict.AllesOk;

        return new Diagnosis(verdict, isWifi, router, internet, server);
    }

    private static async Task<ProbeResult> ProbeAsync(string target, IPAddress? address, CancellationToken token)
    {
        const int count = 5;
        if (address is null) return new ProbeResult(target, null, null, 0, 0);
        using var ping = new Ping();
        var times = new List<long>();
        int lost = 0;
        for (int i = 0; i < count; i++)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var reply = await ping.SendPingAsync(address, 1000);
                if (reply.Status == IPStatus.Success) times.Add(reply.RoundtripTime);
                else lost++;
            }
            catch (PingException)
            {
                lost++;
            }
            await Task.Delay(200, token);
        }
        return new ProbeResult(target, address, times.Count > 0 ? times.Average() : null, lost, count);
    }

    /// <summary>Standard-Gateway der aktiven Verbindung (= dein Router) und ob es WLAN ist.</summary>
    private static (IPAddress? Gateway, bool IsWifi) FindGateway()
    {
        var candidates = NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up
                        && n.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel))
            .Select(n => (Nic: n, Gw: n.GetIPProperties().GatewayAddresses
                .Select(g => g.Address)
                .FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !a.Equals(IPAddress.Any))))
            .Where(x => x.Gw is not null)
            .ToList();
        var best = candidates.FirstOrDefault();
        return (best.Gw, best.Nic?.NetworkInterfaceType == NetworkInterfaceType.Wireless80211);
    }
}
