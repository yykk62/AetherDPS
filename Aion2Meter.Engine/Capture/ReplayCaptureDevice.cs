using AionDpsMeter.Services.PacketCapture;

namespace Aion2Meter.Engine.Capture;

/// <summary>
/// Spielt einen Mitschnitt ab, als käme er live vom Netzwerk.
/// Speed 1 = Echtzeit, 0 = so schnell wie möglich.
/// </summary>
public sealed class ReplayCaptureDevice(TcpStreamBuffer streamBuffer, string filePath, double speed) : IPacketCaptureDevice
{
    private CancellationTokenSource? cts;
    private Task? playback;

    public bool IsCapturing { get; private set; }
    public bool IsFinished { get; private set; }
    public string? DeviceName => $"Mitschnitt: {Path.GetFileName(filePath)}";

    public void DiscoverAdapters() { }

    public void StartCapture()
    {
        if (IsCapturing) return;
        IsCapturing = true;
        IsFinished = false;
        cts = new CancellationTokenSource();
        playback = Task.Run(() => PlayAsync(cts.Token));
    }

    /// <summary>Blockierend abspielen (für Tests und die Kommandozeile).</summary>
    public void PlayToEnd()
    {
        foreach (var chunk in CaptureFile.Read(filePath))
            streamBuffer.AddData(chunk.StreamKey, chunk.Payload, chunk.ArrivedAtMs);
        IsFinished = true;
    }

    private async Task PlayAsync(CancellationToken token)
    {
        long? previous = null;
        try
        {
            foreach (var chunk in CaptureFile.Read(filePath))
            {
                token.ThrowIfCancellationRequested();
                if (speed > 0 && previous is long prev && chunk.ArrivedAtMs > prev)
                    await Task.Delay(TimeSpan.FromMilliseconds((chunk.ArrivedAtMs - prev) / speed), token);
                previous = chunk.ArrivedAtMs;
                streamBuffer.AddData(chunk.StreamKey, chunk.Payload, chunk.ArrivedAtMs);
            }
            IsFinished = true;
        }
        catch (OperationCanceledException) { }
    }

    public void StopCapture()
    {
        if (!IsCapturing) return;
        cts?.Cancel();
        try { playback?.Wait(TimeSpan.FromSeconds(2)); } catch (AggregateException) { }
        cts?.Dispose();
        cts = null;
        IsCapturing = false;
    }

    public void Dispose() => StopCapture();
}
