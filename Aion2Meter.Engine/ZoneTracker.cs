using System.Net;
using System.Text;
using AionDpsMeter.Services.PacketCapture;
using AionDpsMeter.Services.PacketProcessing.Routing;

namespace Aion2Meter.Engine;

/// <summary>
/// Erkennt Dungeons (Instanzen): Beim Betreten schickt der Server mit Opcode 0x3616 die Adresse
/// eines anderen Spielservers, beim Verlassen wieder die der Welt (geführte Aufnahme 14.10.2026:
/// Welt 193.202.112.110, Dungeon 87.232.75.228). Teleports innerhalb der Welt lösen das nicht aus.
/// Den Namen des Dungeons verrät das Paket nicht.
/// </summary>
public sealed class ZoneTracker
{
    public const ushort OpServerSwitch = 0x3616;

    private readonly IPacketCaptureDevice capture;
    private readonly Lock gate = new();
    private string? worldAddress;

    public ZoneTracker(PacketDispatchService dispatch, IPacketCaptureDevice capture)
    {
        this.capture = capture;
        var previous = dispatch.Observer;
        dispatch.Observer = (opcode, data) =>
        {
            previous?.Invoke(opcode, data);
            if (opcode == OpServerSwitch && ParseAddress(data) is { } address) OnServerSwitch(address);
        };
    }

    public bool InInstance { get; private set; }
    public string? InstanceAddress { get; private set; }
    public DateTime? EnteredAt { get; private set; }

    /// <summary>true = Dungeon betreten, false = verlassen.</summary>
    public event Action<bool>? Changed;

    private void OnServerSwitch(string address)
    {
        bool entered;
        lock (gate)
        {
            // Weltadresse: die bekannte Verbindung vor dem ersten Wechsel (live), sonst die Adresse beim Verlassen
            worldAddress ??= (capture as CaptureDevice)?.ServerAddress?.ToString();
            entered = worldAddress is null ? !InInstance : address != worldAddress;
            if (!entered && worldAddress is null) worldAddress = address;
            if (entered == InInstance) return;
            InInstance = entered;
            InstanceAddress = entered ? address : null;
            EnteredAt = entered ? DateTime.Now : null;
        }
        Changed?.Invoke(entered);
    }

    /// <summary>Die IP steht als Text im Paket, z. B. „87.232.75.228“.</summary>
    public static string? ParseAddress(byte[] data)
    {
        var text = Encoding.ASCII.GetString(data.Select(b => b is >= 0x2E and <= 0x39 ? b : (byte)' ').ToArray());
        foreach (var part in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            if (part.Count(c => c == '.') == 3 && IPAddress.TryParse(part, out var ip) && ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                return part;
        return null;
    }
}
