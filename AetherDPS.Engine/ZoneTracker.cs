using System.Net;
using System.Text;
using AionDpsMeter.Services.PacketCapture;
using AionDpsMeter.Services.PacketProcessing.Routing;

namespace AetherDPS.Engine;

/// <summary>
/// Erkennt Dungeons (Instanzen).
/// <list type="bullet">
/// <item>Karte laden (0x3621, Bytes „21 36“, bei jedem Zonenwechsel): <c>&lt;u32 Zähler&gt; &lt;u32 Karten-Id&gt;</c>.
/// Instanz-Karten haben Ids ab 600000 und sind gleich der Dungeon-Id (mit Schwierigkeit), so kennt
/// der Meter den Namen. Erkenntnis aus A2Tools (GPL-3.0, stream_processor.rs).</item>
/// <item>Serverwechsel (0x3616): Beim Betreten kommt die Adresse eines anderen Spielservers, beim
/// Verlassen wieder die der Welt. Rückfall, falls keine Karten-Pakete ankommen.</item>
/// </list>
/// </summary>
public sealed class ZoneTracker
{
    public const ushort OpServerSwitch = 0x3616;
    public const ushort OpMapLoad = 0x3621;

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
            else if (opcode == OpMapLoad && ParseMapId(data) is { } map) OnMapLoad(map);
        };
    }

    public bool InInstance { get; private set; }
    public string? InstanceAddress { get; private set; }
    public DateTime? EnteredAt { get; private set; }

    /// <summary>Zuletzt geladene Karte (0 = noch keine gesehen).</summary>
    public int MapId { get; private set; }

    /// <summary>Id des aktuellen Dungeons (= Karten-Id), sonst 0.</summary>
    public int DungeonId { get; private set; }

    /// <summary>Name in der gewählten Sprache, z. B. „Krao Cave (Schwer)“; null, wenn unbekannt oder in der Welt.</summary>
    public string? DungeonName => DungeonId > 0 ? GameText.DungeonName(DungeonId) : null;

    private bool mapLoadsSeen;

    /// <summary>true = Dungeon betreten, false = verlassen.</summary>
    public event Action<bool>? Changed;

    private void OnMapLoad(int mapId)
    {
        bool entered = GameText.IsInstanceMap(mapId);
        bool changed;
        lock (gate)
        {
            mapLoadsSeen = true;
            MapId = mapId;
            DungeonId = entered ? mapId : 0;
            changed = entered != InInstance;
            if (changed)
            {
                InInstance = entered;
                EnteredAt = entered ? DateTime.Now : null;
                if (!entered) InstanceAddress = null;
            }
        }
        if (changed) Changed?.Invoke(entered);
    }

    private void OnServerSwitch(string address)
    {
        bool entered;
        lock (gate)
        {
            if (mapLoadsSeen)
            {
                // Karten-Pakete sind genauer; die Adresse nur merken
                if (InInstance) InstanceAddress = address;
                return;
            }
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

    /// <summary>Hinter Längen-Varint und Opcode: u32 Zähler, u32 Karten-Id (little endian).</summary>
    public static int? ParseMapId(byte[] data)
    {
        int pos = 0;
        while (pos < data.Length && (data[pos] & 0x80) != 0) pos++;
        pos += 1 + 2 + 4;   // Varint-Ende, Opcode, Zähler
        if (data.Length < pos + 4) return null;
        int id = BitConverter.ToInt32(data, pos);
        return id > 0 ? id : null;
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
