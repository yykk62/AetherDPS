using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace AetherDPS.Engine.Community;

/// <summary>
/// Discord Rich Presence („Spielt AetherDPS – AION 2 · Dungeon: …“), nur mit Zustimmung
/// (<see cref="MeterSettings.DiscordPresence"/>). Spricht ausschließlich mit dem lokal laufenden
/// Discord-Client über die Named Pipe <c>\\.\pipe\discord-ipc-0..9</c> – nichts geht an unsere Server.
/// Discord nicht gestartet: still alle <see cref="RetryInterval"/> neu versuchen. Aktualisiert
/// höchstens alle <see cref="MinUpdateInterval"/> (Discord erlaubt 5 Änderungen je 20 s).
/// </summary>
public sealed class DiscordPresence : IDisposable
{
    public const string ClientId = "1560475211114090566";
    public const string LargeImage = "logo";
    public static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan MinUpdateInterval = TimeSpan.FromSeconds(15);

    private readonly MeterSettings settings;
    private readonly PresenceTracker tracker;
    private readonly ILogger<DiscordPresence> logger;
    private readonly CancellationTokenSource stop = new();
    private DiscordIpc? ipc;
    private PresenceActivity? lastSent;
    private DateTime lastSentAt = DateTime.MinValue;
    private DateTime nextConnect = DateTime.MinValue;
    private Task? loop;

    public DiscordPresence(MeterSettings settings, PresenceTracker tracker, ILogger<DiscordPresence> logger)
    {
        this.settings = settings;
        this.tracker = tracker;
        this.logger = logger;
        // Einstellung geändert → sofort neu versuchen statt bis zu 30 s zu warten
        settings.Changed += (_, _) => nextConnect = DateTime.MinValue;
    }

    /// <summary>Name der Pipe ohne Nummer (Tests nehmen einen eigenen, damit ein laufendes Discord nicht stört).</summary>
    public string PipePrefix { get; set; } = DiscordIpc.DefaultPrefix;

    /// <summary>Gerade an Discord geschickt (null = Anzeige entfernt) – für die Kommandozeile.</summary>
    public event Action<PresenceActivity?>? Sent;

    /// <summary>Antwort von Discord auf einen Befehl (DISPATCH/ERROR) – für die Kommandozeile.</summary>
    public event Action<JsonNode>? Response;

    /// <summary>Mit Discord verbunden (für den Hinweis in den Einstellungen).</summary>
    public bool Connected => ipc?.IsOpen == true;

    public event EventHandler? StatusChanged;

    public void Start() => loop ??= Task.Run(() => RunAsync(stop.Token));

    /// <summary>Ein Durchlauf der Schleife (einmal pro Sekunde; öffentlich für Tests).</summary>
    public void Tick(DateTime now) => Step(now);

    private async Task RunAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try { Step(DateTime.Now); }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Discord Rich Presence");
                Disconnect();
            }
            try { await Task.Delay(1000, token); } catch (OperationCanceledException) { break; }
        }
        Disconnect(clear: true);
    }

    private void Step(DateTime now)
    {
        if (!settings.DiscordPresence)
        {
            if (ipc is not null) Disconnect(clear: true);
            return;
        }
        if (ipc is { IsOpen: false }) Disconnect();
        if (ipc is null)
        {
            if (now < nextConnect) return;
            nextConnect = now + RetryInterval;
            ipc = DiscordIpc.TryConnect(ClientId, PipePrefix);
            if (ipc is null) return;
            ipc.Response += OnResponse;
            logger.LogInformation("Discord Rich Presence verbunden");
            lastSent = null;
            lastSentAt = DateTime.MinValue;
            StatusChanged?.Invoke(this, EventArgs.Empty);
        }
        var activity = PresenceText.Build(tracker.Current(now), settings.DiscordPresenceNames);
        if (activity == lastSent || now - lastSentAt < MinUpdateInterval) return;
        ipc.SetActivity(BuildActivity(activity));
        lastSent = activity;
        lastSentAt = now;
        Sent?.Invoke(activity);
    }

    private void OnResponse(JsonNode json)
    {
        if (json["evt"]?.GetValue<string>() == "ERROR")
            logger.LogWarning("Discord Rich Presence: {Fehler}", json["data"]?["message"]?.GetValue<string>());
        Response?.Invoke(json);
    }

    private void Disconnect(bool clear = false)
    {
        var old = ipc;
        if (old is null) return;
        ipc = null;
        try
        {
            if (clear && old.IsOpen)
            {
                old.SetActivity(null);
                Sent?.Invoke(null);
            }
        }
        catch (Exception) { }
        old.Dispose();
        StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Das <c>activity</c>-Objekt für SET_ACTIVITY.</summary>
    public static JsonObject BuildActivity(PresenceActivity a)
    {
        var activity = new JsonObject
        {
            ["type"] = 0,
            ["details"] = a.Details,
            ["timestamps"] = new JsonObject { ["start"] = new DateTimeOffset(a.Since).ToUnixTimeSeconds() },
            ["assets"] = new JsonObject { ["large_image"] = LargeImage, ["large_text"] = a.LargeText },
            ["buttons"] = new JsonArray(
                new JsonObject { ["label"] = a.DownloadLabel, ["url"] = PresenceText.DownloadUrl },
                new JsonObject { ["label"] = a.DiscordLabel, ["url"] = PresenceText.DiscordUrl }),
        };
        if (a.State is { Length: >= 2 }) activity["state"] = a.State;
        return activity;
    }

    public void Dispose()
    {
        stop.Cancel();
        try { loop?.Wait(TimeSpan.FromSeconds(2)); } catch (AggregateException) { }
        Disconnect(clear: true);
    }
}

/// <summary>
/// Das lokale Discord-IPC-Protokoll: Rahmen aus <c>u32 op</c>, <c>u32 Länge</c> (little endian) und JSON.
/// op 0 = Handshake <c>{v:1, client_id}</c>, 1 = Befehl/Antwort, 2 = Schließen, 3/4 = Ping/Pong.
/// </summary>
public sealed class DiscordIpc : IDisposable
{
    public const int OpHandshake = 0, OpFrame = 1, OpClose = 2, OpPing = 3, OpPong = 4;
    public const string DefaultPrefix = "discord-ipc-";

    private readonly NamedPipeClientStream pipe;
    private readonly Lock writeGate = new();
    private volatile bool open = true;
    private int nonce;

    private DiscordIpc(NamedPipeClientStream pipe) => this.pipe = pipe;

    public bool IsOpen => open && pipe.IsConnected;

    /// <summary>Antworten auf Befehle (op 1).</summary>
    public event Action<JsonNode>? Response;

    /// <summary>Probiert discord-ipc-0 bis -9 und macht den Handshake. null, wenn Discord nicht läuft.</summary>
    public static DiscordIpc? TryConnect(string clientId, string prefix = DefaultPrefix)
    {
        for (int i = 0; i < 10; i++)
        {
            var pipe = new NamedPipeClientStream(".", prefix + i, PipeDirection.InOut, PipeOptions.Asynchronous);
            try
            {
                pipe.Connect(50);   // gibt es die Pipe, geht es sofort
                var ipc = new DiscordIpc(pipe);
                ipc.Write(OpHandshake, new JsonObject { ["v"] = 1, ["client_id"] = clientId });
                // Antwort: DISPATCH/READY – sonst (z. B. falsche Client-ID) schließt Discord mit op 2
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                var (op, json) = ReadFrame(pipe, timeout.Token);
                if (op != OpFrame || json?["evt"]?.GetValue<string>() != "READY")
                {
                    ipc.Dispose();
                    continue;
                }
                _ = Task.Run(ipc.ReadLoop);
                return ipc;
            }
            catch (Exception ex) when (ex is TimeoutException or IOException or OperationCanceledException or UnauthorizedAccessException or System.Text.Json.JsonException)
            {
                pipe.Dispose();
            }
        }
        return null;
    }

    /// <summary>SET_ACTIVITY; <paramref name="activity"/> null = Anzeige entfernen.</summary>
    public void SetActivity(JsonObject? activity) => Write(OpFrame, new JsonObject
    {
        ["cmd"] = "SET_ACTIVITY",
        ["args"] = new JsonObject { ["pid"] = Environment.ProcessId, ["activity"] = activity },
        ["nonce"] = Interlocked.Increment(ref nonce).ToString(),
    });

    /// <summary>Antworten lesen, damit die Pipe nicht vollläuft; auf Ping antworten, bei Schließen aufhören.</summary>
    private void ReadLoop()
    {
        try
        {
            while (open)
            {
                var (op, json) = ReadFrame(pipe, CancellationToken.None);
                if (op == OpPing && json is not null) Write(OpPong, json);
                else if (op == OpClose) break;
                else if (op == OpFrame && json is not null) Response?.Invoke(json);
            }
        }
        catch (Exception) { }
        open = false;
    }

    private void Write(int op, JsonNode payload)
    {
        var frame = Frame(op, payload.ToJsonString());
        lock (writeGate)
        {
            pipe.Write(frame);
            pipe.Flush();
        }
    }

    public static byte[] Frame(int op, string json)
    {
        var body = Encoding.UTF8.GetBytes(json);
        var frame = new byte[8 + body.Length];
        BinaryPrimitives.WriteInt32LittleEndian(frame, op);
        BinaryPrimitives.WriteInt32LittleEndian(frame.AsSpan(4), body.Length);
        body.CopyTo(frame, 8);
        return frame;
    }

    public static (int Op, JsonNode? Json) ReadFrame(Stream stream, CancellationToken token)
    {
        var header = new byte[8];
        stream.ReadExactlyAsync(header, token).AsTask().GetAwaiter().GetResult();
        int op = BinaryPrimitives.ReadInt32LittleEndian(header);
        int length = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(4));
        if (length is < 0 or > 1 << 20) throw new IOException("Discord-IPC: ungültige Länge");
        var body = new byte[length];
        stream.ReadExactlyAsync(body, token).AsTask().GetAwaiter().GetResult();
        return (op, length == 0 ? null : JsonNode.Parse(body));
    }

    public void Dispose()
    {
        open = false;
        pipe.Dispose();
    }
}
