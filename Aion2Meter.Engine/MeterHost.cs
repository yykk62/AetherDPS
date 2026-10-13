using Aion2Meter.Engine.Capture;
using AionDpsMeter.Services.Models;
using AionDpsMeter.Services.PacketCapture;
using AionDpsMeter.Services.Services.Session;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aion2Meter.Engine;

/// <summary>Kopflose Variante der Pipeline – für Tests und die Kommandozeile.</summary>
public sealed class MeterHost : IDisposable
{
    private readonly ServiceProvider provider;

    public MeterHost(MeterOptions options, Action<ILoggingBuilder>? logging = null)
    {
        Directory.CreateDirectory(options.DataDir);
        Environment.CurrentDirectory = options.DataDir;
        GermanGameData.Apply();
        var services = new ServiceCollection();
        services.AddLogging(b =>
        {
            b.SetMinimumLevel(LogLevel.Warning);
            logging?.Invoke(b);
        });
        services.AddAion2Meter(options);
        provider = services.BuildServiceProvider();

        Pipeline = provider.GetRequiredService<IPacketService>();
        Recorder = provider.GetRequiredService<RingRecorder>();
    }

    public IPacketService Pipeline { get; }
    public RingRecorder Recorder { get; }
    public TcpStreamBuffer StreamBuffer => provider.GetRequiredService<TcpStreamBuffer>();
    public CombatSessionManager Sessions => provider.GetRequiredService<CombatSessionManager>();
    public IPacketCaptureDevice Capture => provider.GetRequiredService<IPacketCaptureDevice>();
    public T Get<T>() where T : notnull => provider.GetRequiredService<T>();

    public void Feed(byte[] data, long arrivedAtMs, string streamKey = "test:1->test:2") =>
        StreamBuffer.AddData(streamKey, data, arrivedAtMs);

    public void Dispose() => provider.Dispose();
}
