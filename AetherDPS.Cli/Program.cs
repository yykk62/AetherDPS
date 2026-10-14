using System.Globalization;
using AetherDPS.Engine;
using AetherDPS.Engine.Capture;
using AetherDPS.Engine.Testing;

// Werkzeug für Wartung ohne Spiel:
//   aetherdps-cli replay <mitschnitt.txt>   – Mitschnitt auswerten, Tabelle ausgeben
//   aetherdps-cli demo <ziel.txt>           – künstlichen Kampf erzeugen (für UI-Tests)

Console.OutputEncoding = System.Text.Encoding.UTF8;
var de = CultureInfo.GetCultureInfo("de-DE");
if (args.Length < 2)
{
    Console.WriteLine("Aufruf: aetherdps-cli replay <mitschnitt.txt> | demo <ziel.txt>");
    return 1;
}

var file = Path.GetFullPath(args[1]);
switch (args[0])
{
    case "replay":
        return Replay(file);
    case "analyze":
        return Analyze(file);
    case "research":
        return Research.Run(file, args.Length > 2 ? args[2] : Path.Combine(Path.GetTempPath(), "aetherdps-research"));
    case "dump":
        return Dump(file, args.Length > 2 ? args[2] : null);
    case "live":
        return Live(file, args.Length > 2 ? int.Parse(args[2]) : 120);
    case "demo":
        WriteDemo(file);
        Console.WriteLine($"Demo-Mitschnitt geschrieben: {file}");
        return 0;
    default:
        Console.WriteLine($"Unbekannter Befehl: {args[0]}");
        return 1;
}

int Replay(string path)
{
    var dataDir = Path.Combine(Path.GetTempPath(), "aetherdps-cli");
    using var host = new MeterHost(new MeterOptions { DataDir = dataDir, ReplayFile = path, ReplaySpeed = 0 });
    var device = (ReplayCaptureDevice)host.Capture;
    device.PlayToEnd();

    var target = host.Sessions.GetActiveTargetInfo();
    Console.WriteLine($"Ziel: {(target is null ? "–" : GameText.MobName(target))}");
    var stats = host.Sessions.PlayerStats.OrderByDescending(p => p.TotalDamage).ToList();
    if (stats.Count == 0)
    {
        Console.WriteLine("Kein Schaden erkannt.");
        return 2;
    }

    Console.WriteLine($"{"Spieler",-22} {"Klasse",-14} {"Schaden",14} {"Anteil",7}");
    foreach (var p in stats)
    {
        Console.WriteLine($"{p.PlayerName,-22} {GameText.ClassName(p.ClassId, p.ClassName),-14} " +
                          $"{p.TotalDamage.ToString("N0", de),14} {p.DamagePercentage.ToString("F1", de),6}%");
        foreach (var s in host.Sessions.GetPlayerSkillStats(p.PlayerId).OrderByDescending(s => s.TotalDamage).Take(3))
            Console.WriteLine($"    {s.SkillName,-30} {s.TotalDamage.ToString("N0", de),12}  {s.HitCount}×");
    }
    return 0;
}

// Diagnose: welche Opcodes kommen an, werden Buffs/Abklingzeiten/eigener Charakter erkannt?
int Analyze(string path)
{
    var dataDir = Path.Combine(Path.GetTempPath(), "aetherdps-cli");
    using var host = new MeterHost(new MeterOptions { DataDir = dataDir, ReplayFile = path, ReplaySpeed = 0 });
    var opcodes = new Dictionary<ushort, int>();
    host.Mode.Activate();   // vorher: TrackingMode setzt beim Erzeugen seinen eigenen Filter
    host.Get<AionDpsMeter.Services.PacketProcessing.Routing.PacketDispatchService>().Gate = op =>
    {
        opcodes[op] = opcodes.GetValueOrDefault(op) + 1;
        return true;
    };
    int frames = 0; long frameBytes = 0;
    host.StreamBuffer.PacketExtracted += (_, e) => { frames++; frameBytes += e.Payload.Length; };
    var sessions = host.Sessions;
    var entities = host.Get<AionDpsMeter.Services.Services.Entity.EntityTracker>();
    int buffs = 0, ownBuffs = 0, cds = 0;
    var buffTargets = new Dictionary<int, int>();
    var buffNames = new Dictionary<string, int>();
    sessions.BuffRecorded += b =>
    {
        buffs++;
        if (entities.IsCurrentPlayer(b.EntityId)) ownBuffs++;
        buffTargets[b.EntityId] = buffTargets.GetValueOrDefault(b.EntityId) + 1;
        buffNames[b.BuffName] = buffNames.GetValueOrDefault(b.BuffName) + 1;
    };
    var cdNames = new Dictionary<string, int>();
    sessions.SkillCooldownRecorded += c =>
    {
        cds++;
        cdNames[$"{c.Name} ({c.Duration.TotalSeconds:0.#} s)"] = cdNames.GetValueOrDefault($"{c.Name} ({c.Duration.TotalSeconds:0.#} s)") + 1;
    };
    ((ReplayCaptureDevice)host.Capture).PlayToEnd();

    long raw = CaptureFile.Read(path).Sum(c => (long)c.Payload.Length);
    Console.WriteLine($"Rohdaten: {raw:N0} Byte, davon als Pakete erkannt: {frameBytes:N0} Byte in {frames} Paketen");
    Console.WriteLine("Opcodes:");
    foreach (var (op, n) in opcodes.OrderByDescending(x => x.Value).Take(25))
        Console.WriteLine($"  0x{op:X4}  {n,6}");
    var user = sessions.PlayerStats.FirstOrDefault(p => p.IsUser);
    Console.WriteLine($"Eigener Charakter erkannt: {(user is null ? "nein (in Kampfstatistik)" : user.PlayerName)}");
    Console.WriteLine($"Buff-Ereignisse: {buffs}, davon auf dir: {ownBuffs}");
    foreach (var (id, n) in buffTargets.OrderByDescending(x => x.Value).Take(8))
    {
        var p = entities.GetPlayerEntity(id);
        Console.WriteLine($"  Ziel {id,-8} {n,4}×  {(p is null ? "?" : $"{p.Name} user={p.IsUser}")}");
    }
    foreach (var (name, n) in buffNames.OrderByDescending(x => x.Value).Take(10))
        Console.WriteLine($"  Buff {name,-35} {n,4}×");
    Console.WriteLine($"Abklingzeit-Ereignisse: {cds}");
    foreach (var (name, n) in cdNames.OrderByDescending(x => x.Value).Take(10))
        Console.WriteLine($"  {name,-45} {n,4}×");
    return 0;
}

static void WriteDemo(string path)
{
    // Neun Spieler (alle Klassen) gegen den Sandsack (2000000), 90 s, ein Treffer pro Spieler alle ~1,2 s.
    var party = new (int Id, int Skill, int Base)[]
    {
        (101, 11000001, 9_000),   // Gladiator
        (102, 14000001, 8_200),   // Waldläufer
        (103, 15000001, 10_500),  // Magier
        (104, 17000001, 3_000),   // Kleriker
        (105, 12000001, 5_500),   // Templer
        (106, 13000001, 9_800),   // Assassine
        (107, 16000001, 8_700),   // Beschwörer
        (108, 18000001, 4_200),   // Kantor
        (109, 19000001, 9_300),   // Faustkämpfer
    };
    // Ziel ist ein Boss (Ruthilis des Schmerzes) mit Lebensbalken, damit Kampfkarte und Auto-Modus etwas zeigen.
    const int boss = 7001, ruthilis = 2300708;
    const long maxHp = 4_000_000;
    long hp = maxHp;
    var rng = new Random(42);
    var chunks = new List<RawChunk>();
    long start = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    chunks.Add(new RawChunk(start, "demo", SyntheticPackets.Heartbeat()));
    chunks.Add(new RawChunk(start + 100, "demo", SyntheticPackets.MobInfo(boss, ruthilis, maxHp)));

    for (long t = 0; t < 90_000; t += 300)
    {
        var (id, skill, baseDmg) = party[(int)(t / 300 % party.Length)];
        bool crit = rng.NextDouble() < 0.25;
        long dmg = (long)(baseDmg * (0.8 + rng.NextDouble() * 0.4) * (crit ? 1.8 : 1));
        chunks.Add(new RawChunk(start + 200 + t, "demo", SyntheticPackets.Damage(boss, id, skill, dmg, crit)));
        hp = Math.Max(1, hp - dmg);
        chunks.Add(new RawChunk(start + 210 + t, "demo", SyntheticPackets.RemainHp(boss, hp)));
        if (t % 5_000 == 0)
            chunks.Add(new RawChunk(start + 250 + t, "demo", SyntheticPackets.Heartbeat()));
    }
    CaptureFile.Write(path, chunks, ["Demo – künstlich erzeugt, kein echter Spielverkehr"]);
}

// Diagnose: alle (entpackten) Pakete durchsuchen – z. B. nach dem eigenen Charakternamen.
static int Dump(string path, string? needle)
{
    var dataDir = Path.Combine(Path.GetTempPath(), "aetherdps-cli");
    using var host = new MeterHost(new MeterOptions { DataDir = dataDir, ReplayFile = path, ReplaySpeed = 0 });
    var processor = host.Get<AionDpsMeter.Services.PacketProcessing.PacketProcessor>();
    var process = typeof(AionDpsMeter.Services.PacketProcessing.PacketProcessor).GetMethod("ProcessPacket",
        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
    var needleBytes = needle is null ? null : System.Text.Encoding.UTF8.GetBytes(needle);
    var counts = new Dictionary<ushort, int>();
    int hits = 0;
    host.StreamBuffer.PacketExtracted += (_, e) =>
    {
        var frames = (System.Collections.IEnumerable)process.Invoke(processor, [e.Payload])!;
        foreach (AionDpsMeter.Services.PacketProcessing.Routing.Packet frame in frames)
        {
            var data = frame.Data;
            int pos = 0;
            while (pos < data.Length && (data[pos] & 0x80) != 0) pos++;
            pos++;
            if (pos + 2 > data.Length) continue;
            ushort op = (ushort)(data[pos] | (data[pos + 1] << 8));
            counts[op] = counts.GetValueOrDefault(op) + 1;
            if (needleBytes is not null && data.AsSpan().IndexOf(needleBytes) >= 0 && hits++ < 20)
                Console.WriteLine($"TREFFER 0x{op:X4} len={data.Length}: {Convert.ToHexString(data.AsSpan(0, Math.Min(data.Length, 120)))}");
        }
    };
    ((ReplayCaptureDevice)host.Capture).PlayToEnd();
    Console.WriteLine($"Treffer: {hits}");
    foreach (var (op, n) in counts.OrderByDescending(x => x.Value).Take(30)) Console.WriteLine($"  0x{op:X4} {n,6}");
    return 0;
}

// Live mitlesen (ohne Fenster, parallel zum Meter): Feldboss-Liste, Karte, Zeitlimit melden, danach Mitschnitt speichern.
// Der Mitschnitt enthält Spielernamen – nur lokal auswerten, nie committen.
static int Live(string path, int seconds)
{
    var dataDir = Path.Combine(Path.GetTempPath(), "aetherdps-cli-live");
    using var host = new MeterHost(new MeterOptions { DataDir = dataDir });
    var dispatch = host.Get<AionDpsMeter.Services.PacketProcessing.Routing.PacketDispatchService>();
    var counts = new Dictionary<ushort, int>();
    var previous = dispatch.Observer;
    dispatch.Observer = (op, data) =>
    {
        previous?.Invoke(op, data);
        lock (counts) counts[op] = counts.GetValueOrDefault(op) + 1;
        if (op == FieldBosses.OpList)
        {
            var list = FieldBosses.Parse(data);
            Console.WriteLine($"{DateTime.Now:HH:mm:ss} 0x9101 len={data.Length} → {(list is null ? "NICHT LESBAR " + Convert.ToHexString(data.AsSpan(0, Math.Min(64, data.Length))) : $"Karte {list.Map}, {list.Entries.Count} Bosse")}");
        }
        if (op == ZoneTracker.OpMapLoad) Console.WriteLine($"{DateTime.Now:HH:mm:ss} Karte {ZoneTracker.ParseMapId(data)}");
    };
    host.Pipeline.Start();
    Console.WriteLine($"Lese {seconds} s mit …");
    for (int s = 0; s < seconds; s += 5)
    {
        Thread.Sleep(TimeSpan.FromSeconds(5));
        Console.WriteLine($"{DateTime.Now:HH:mm:ss} Server: {(host.Capture as AionDpsMeter.Services.PacketCapture.CaptureDevice)?.ServerAddress?.ToString() ?? "–"}");
    }
    host.Pipeline.Stop();
    var saved = host.Recorder.SaveSnapshot(Path.GetDirectoryName(path)!, "live", "cli");
    Console.WriteLine($"Pakete: {counts.Values.Sum()}, Opcodes: {counts.Count}, 0x9101: {counts.GetValueOrDefault(FieldBosses.OpList)}");
    Console.WriteLine("Häufigste: " + string.Join(", ", counts.OrderByDescending(c => c.Value).Take(12).Select(c => $"{c.Key:X4}×{c.Value}")));
    Console.WriteLine($"Mitschnitt: {saved ?? "–"}");
    return 0;
}
