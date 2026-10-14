using Aion2Meter.Engine;
using Aion2Meter.Engine.Capture;
using AionDpsMeter.Services.PacketProcessing.Routing;
using AionDpsMeter.Services.Services.Entity;

/// <summary>
/// Auswertung einer geführten Aufnahme: welche Opcodes in welchem Abschnitt (zwischen zwei F8-Marken)
/// auftauchen, welche nur selten vorkommen, und was die Pipeline daraus erkennt (Charakter, Gruppe,
/// Tode, Bosse). Beispiele je Opcode landen in einem Ordner zum Nachsehen.
/// Die Marke kommt NACH der Aktion – ein Ereignis gehört zum Abschnitt VOR seiner Marke.
/// </summary>
static class Research
{
    public static int Run(string path, string outDir)
    {
        var marks = ResearchRecorder.ReadMarks(path).ToList();
        var bounds = marks.Select(m => (At: new DateTimeOffset(DateTime.SpecifyKind(m.At, DateTimeKind.Utc)).ToUnixTimeMilliseconds(), m.Key)).ToList();
        string SegmentOf(long ms)
        {
            // Abschnitt = die nächste Marke danach (die Aktion passierte vor dem F8)
            foreach (var b in bounds) if (ms <= b.At) return b.Key;
            return "nach_ende";
        }

        var dataDir = Path.Combine(Path.GetTempPath(), "aion2meter-cli-research");
        using var host = new MeterHost(new MeterOptions { DataDir = dataDir });
        var processor = host.Get<AionDpsMeter.Services.PacketProcessing.PacketProcessor>();
        var split = typeof(AionDpsMeter.Services.PacketProcessing.PacketProcessor).GetMethod("ProcessPacket",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;

        long now = 0;
        var perSegment = new Dictionary<string, Dictionary<ushort, int>>();
        var total = new Dictionary<ushort, int>();
        var first = new Dictionary<ushort, long>();
        var samples = new Dictionary<(ushort, string), List<string>>();
        // Optional alle Pakete eines Zeitfensters (lokale Uhrzeit), z. B. AION2METER_WINDOW=02:46:40-02:46:56
        var window = Environment.GetEnvironmentVariable("AION2METER_WINDOW")?.Split('-');
        var windowLines = new List<string>();
        // Optional komplette Pakete bestimmter Opcodes, z. B. AION2METER_FULL=3633,3616
        var fullOps = (Environment.GetEnvironmentVariable("AION2METER_FULL") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(o => Convert.ToUInt16(o, 16)).ToHashSet();
        var fullLines = new List<string>();
        host.StreamBuffer.PacketExtracted += (_, e) =>
        {
            foreach (Packet frame in (System.Collections.IEnumerable)split.Invoke(processor, [e.Payload])!)
            {
                var data = frame.Data;
                int pos = 0;
                while (pos < data.Length && (data[pos] & 0x80) != 0) pos++;
                pos++;
                if (pos + 2 > data.Length) continue;
                ushort op = (ushort)(data[pos] | (data[pos + 1] << 8));
                var seg = SegmentOf(now);
                if (!perSegment.TryGetValue(seg, out var counts)) perSegment[seg] = counts = [];
                counts[op] = counts.GetValueOrDefault(op) + 1;
                total[op] = total.GetValueOrDefault(op) + 1;
                first.TryAdd(op, now);
                if (fullOps.Contains(op)) fullLines.Add($"{Local(now):HH:mm:ss.fff} [{SegmentOf(now)}] {op:X4} {Convert.ToHexString(data)}");
                if (window is { Length: 2 } && string.CompareOrdinal(Local(now).ToString("HH:mm:ss"), window[0]) >= 0
                    && string.CompareOrdinal(Local(now).ToString("HH:mm:ss"), window[1]) <= 0)
                    windowLines.Add($"{Local(now):HH:mm:ss.fff} {op:X4} len={data.Length} {Convert.ToHexString(data.AsSpan(0, Math.Min(data.Length, 160)))}");
                if (!samples.TryGetValue((op, seg), out var list)) samples[(op, seg)] = list = [];
                if (list.Count < 4) list.Add($"{Local(now):HH:mm:ss.fff} len={data.Length} {Convert.ToHexString(data.AsSpan(0, Math.Min(data.Length, 200)))}");
            }
        };

        // Was die Pipeline erkennt
        var entities = host.Get<EntityTracker>();
        var roster = host.Get<PartyRoster>();
        var events = new List<(long Ms, string Text)>();
        entities.UserIdentified += (id, name, server) => events.Add((now, $"Eigener Charakter: {name} (#{id}) Server „{server}“"));
        roster.Updated += (_, _) => events.Add((now, "Gruppe: " + string.Join(", ", roster.Members.Select(m => $"{m.Name} [{m.ServerName}] Kk {m.CombatPower}"))));
        entities.EntityDied += id =>
        {
            var player = entities.GetPlayerEntity(id);
            var mob = entities.GetTargetMob(id);
            if (player is not null) events.Add((now, $"Tod Spieler: {player.Name}{(player.IsUser ? $" (DU, #{id}, SelfId {host.Get<SelfIdentity>().SelfId})" : "")}"));
            else if (mob is { MobCode: > 0 } && mob.IsBoss) events.Add((now, $"Tod Boss: {GermanGameData.MobName(mob)}"));
        };
        var bossHits = new Dictionary<int, (string Name, long First, long Last, long Damage, int Hits)>();
        // Alle Treffer (auch Mobs) mit Paketzeit – zum Abgleich mit Stagger-Leiste usw.
        var hitLines = new List<string>();
        host.Sessions.RawDamage += (target, actor, skill, damage) =>
        {
            var player = entities.GetPlayerEntity(actor);
            var skillName = AionDpsMeter.Core.GameData.Services.GameDataProvider.Instance.GetSkillOrDefault(skill)?.Name ?? "?";
            hitLines.Add($"{Local(now):HH:mm:ss.fff}	{target}	{actor}	{player?.Name ?? "-"}	{skill}	{skillName}	{damage}");
        };
        host.Sessions.DamageRecorded += hit =>
        {
            var mob = entities.GetTargetMob(hit.TargetEntity.Id);
            if (mob is not { MobCode: > 0 } || !mob.IsBoss) return;
            var b = bossHits.GetValueOrDefault(hit.TargetEntity.Id, (GermanGameData.MobName(mob), now, now, 0, 0));
            bossHits[hit.TargetEntity.Id] = (b.Item1, b.Item2, now, b.Item4 + hit.Damage, b.Item5 + 1);
        };

        var zones = host.Get<ZoneTracker>();
        zones.Changed += entered => events.Add((now, entered ? $"Dungeon betreten ({zones.InstanceAddress})" : "Dungeon verlassen"));
        var recap = host.Get<DeathRecap>();
        recap.Clock = () => Local(now);
        recap.Died += r => events.Add((now, $"Todes-Rückblick: {r.TotalDamage:N0} Schaden, " +
            string.Join("; ", r.Hits.Take(3).Select(h => $"{h.Source} / {h.Skill} {h.Damage:N0} ({h.Count}×)"))));
        var buffs = host.Get<BuffWatcher>();
        host.Sessions.BuffRecorded += b =>
        {
            if (b.EntityId == host.Get<SelfIdentity>().SelfId && b.DurationMs >= 300_000)
                events.Add((now, $"Langer Buff: {b.BuffName} ({b.DurationMs / 60000} Min.)"));
        };

        var stagger = host.Get<StaggerTracker>();
        stagger.Clock = () => Local(now);
        stagger.Announced += boss => events.Add((now, $"Stagger-Phase angekündigt: {boss}"));
        stagger.Finished += p => events.Add((now, $"Stagger {(p.Success ? "GESCHAFFT" : "VERFEHLT")} {p.BossName} {p.Duration.TotalSeconds:0.0} s, Rest {p.Remaining}/{p.Max}: " +
            string.Join(", ", p.Shares.Select(s => $"{s.Name} {s.Points} [{string.Join(" ", s.Skills.Take(4).Select(k => $"{k.Skill} {k.Points}"))}]"))));

        foreach (var chunk in CaptureFile.Read(path))
        {
            now = chunk.ArrivedAtMs;
            host.Feed(chunk.Payload, chunk.ArrivedAtMs, chunk.StreamKey);
            stagger.Tick();
        }

        Directory.CreateDirectory(outDir);
        using var report = new StreamWriter(Path.Combine(outDir, "bericht.txt"));
        void Line(string s) { Console.WriteLine(s); report.WriteLine(s); }

        Line("== Marken (Abschnitt endet mit der Marke) ==");
        foreach (var m in marks) Line($"{m.At.ToLocalTime():HH:mm:ss} {m.Key,-13} {m.Result,-12} {m.Text}");

        Line("\n== Erkannt ==");
        foreach (var (ms, text) in events) Line($"{Local(ms):HH:mm:ss} [{SegmentOf(ms)}] {text}");
        Line($"Schaden auf dich (Treffer): {recap.IncomingHits}");
        foreach (var (id, b) in bossHits)
            Line($"Boss {b.Name} (#{id}): {Local(b.First):HH:mm:ss}–{Local(b.Last):HH:mm:ss}, {b.Hits} Treffer, {b.Damage:N0} Schaden");

        Line("\n== Seltene Opcodes (in höchstens 3 Abschnitten) ==");
        foreach (var (op, n) in total.OrderBy(x => first[x.Key]))
        {
            var segs = perSegment.Where(s => s.Value.ContainsKey(op)).Select(s => $"{s.Key}:{s.Value[op]}").ToList();
            if (segs.Count <= 3) Line($"0x{op:X4} gesamt {n,5}  zuerst {Local(first[op]):HH:mm:ss}  → {string.Join(", ", segs)}");
        }

        Line("\n== Alle Opcodes je Abschnitt ==");
        foreach (var (seg, counts) in perSegment)
            Line($"[{seg}] " + string.Join(" ", counts.OrderByDescending(c => c.Value).Select(c => $"{c.Key:X4}:{c.Value}")));

        foreach (var ((op, seg), list) in samples)
            File.AppendAllLines(Path.Combine(outDir, $"op_{op:X4}.txt"), list.Select(l => $"[{seg}] {l}"));
        File.WriteAllLines(Path.Combine(outDir, "treffer.tsv"), hitLines);
        if (fullLines.Count > 0) File.WriteAllLines(Path.Combine(outDir, "voll.txt"), fullLines);
        if (windowLines.Count > 0) File.WriteAllLines(Path.Combine(outDir, "fenster.txt"), windowLines);
        Line($"\nBeispiele je Opcode: {outDir}");
        return 0;
    }

    private static DateTime Local(long ms) => DateTimeOffset.FromUnixTimeMilliseconds(ms).LocalDateTime;
}
