using AetherDPS.Engine.Capture;
using AionDpsMeter.Services.Models;
using AionDpsMeter.Services.PacketCapture;
using AionDpsMeter.Services.Services.Session;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AetherDPS.Engine;

/// <summary>Kopflose Variante der Pipeline – für Tests und die Kommandozeile.</summary>
public sealed class MeterHost : IDisposable
{
    private readonly ServiceProvider provider;

    public MeterHost(MeterOptions options, Action<ILoggingBuilder>? logging = null)
    {
        Directory.CreateDirectory(options.DataDir);
        Environment.CurrentDirectory = options.DataDir;
        GameText.Apply();
        var services = new ServiceCollection();
        services.AddLogging(b =>
        {
            b.SetMinimumLevel(LogLevel.Warning);
            logging?.Invoke(b);
        });
        services.AddAetherDPS(options);
        provider = services.BuildServiceProvider();

        Pipeline = provider.GetRequiredService<IPacketService>();
        Recorder = provider.GetRequiredService<RingRecorder>();
    }

    public IPacketService Pipeline { get; }
    public RingRecorder Recorder { get; }
    public TcpStreamBuffer StreamBuffer => provider.GetRequiredService<TcpStreamBuffer>();
    public CombatSessionManager Sessions => provider.GetRequiredService<CombatSessionManager>();
    public IPacketCaptureDevice Capture => provider.GetRequiredService<IPacketCaptureDevice>();

    /// <summary>Erst beim ersten Zugriff aktiv – ohne Zugriff läuft alles ungefiltert.</summary>
    public TrackingMode Mode => provider.GetRequiredService<TrackingMode>();
    public RunTracker Run => provider.GetRequiredService<RunTracker>();
    public T Get<T>() where T : notnull => provider.GetRequiredService<T>();

    public void Feed(byte[] data, long arrivedAtMs, string streamKey = "test:1->test:2") =>
        StreamBuffer.AddData(streamKey, data, arrivedAtMs);

    public void Dispose() => provider.Dispose();
}
