using System.Globalization;
using Aion2Meter.Engine;
using Aion2Meter.Engine.Capture;
using Aion2Meter.Engine.Testing;

// Werkzeug für Wartung ohne Spiel:
//   aion2meter-cli replay <mitschnitt.txt>   – Mitschnitt auswerten, Tabelle ausgeben
//   aion2meter-cli demo <ziel.txt>           – künstlichen Kampf erzeugen (für UI-Tests)

Console.OutputEncoding = System.Text.Encoding.UTF8;
var de = CultureInfo.GetCultureInfo("de-DE");
if (args.Length < 2)
{
    Console.WriteLine("Aufruf: aion2meter-cli replay <mitschnitt.txt> | demo <ziel.txt>");
    return 1;
}

var file = Path.GetFullPath(args[1]);
switch (args[0])
{
    case "replay":
        return Replay(file);
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
    var dataDir = Path.Combine(Path.GetTempPath(), "aion2meter-cli");
    using var host = new MeterHost(new MeterOptions { DataDir = dataDir, ReplayFile = path, ReplaySpeed = 0 });
    var device = (ReplayCaptureDevice)host.Capture;
    device.PlayToEnd();

    var target = host.Sessions.GetActiveTargetInfo();
    Console.WriteLine($"Ziel: {(target is null ? "–" : GermanGameData.MobName(target))}");
    var stats = host.Sessions.PlayerStats.OrderByDescending(p => p.TotalDamage).ToList();
    if (stats.Count == 0)
    {
        Console.WriteLine("Kein Schaden erkannt.");
        return 2;
    }

    Console.WriteLine($"{"Spieler",-22} {"Klasse",-14} {"Schaden",14} {"Anteil",7}");
    foreach (var p in stats)
    {
        Console.WriteLine($"{p.PlayerName,-22} {GermanGameData.ClassName(p.ClassId, p.ClassName),-14} " +
                          $"{p.TotalDamage.ToString("N0", de),14} {p.DamagePercentage.ToString("F1", de),6}%");
        foreach (var s in host.Sessions.GetPlayerSkillStats(p.PlayerId).OrderByDescending(s => s.TotalDamage).Take(3))
            Console.WriteLine($"    {s.SkillName,-30} {s.TotalDamage.ToString("N0", de),12}  {s.HitCount}×");
    }
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
    const int dummy = 7001;
    var rng = new Random(42);
    var chunks = new List<RawChunk>();
    long start = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    chunks.Add(new RawChunk(start, "demo", SyntheticPackets.Heartbeat()));

    for (long t = 0; t < 90_000; t += 300)
    {
        var (id, skill, baseDmg) = party[(int)(t / 300 % party.Length)];
        bool crit = rng.NextDouble() < 0.25;
        long dmg = (long)(baseDmg * (0.8 + rng.NextDouble() * 0.4) * (crit ? 1.8 : 1));
        chunks.Add(new RawChunk(start + 200 + t, "demo", SyntheticPackets.Damage(dummy, id, skill, dmg, crit)));
        if (t % 5_000 == 0)
            chunks.Add(new RawChunk(start + 250 + t, "demo", SyntheticPackets.Heartbeat()));
    }
    CaptureFile.Write(path, chunks, ["Demo – künstlich erzeugt, kein echter Spielverkehr"]);
}
