# AION-2-Protokoll (Server → Client)

Stand: Oktober 2026, ausgelesen aus RATmeter, NOIA2 und a2kit. Die maßgebliche Wahrheit ist der Code in
`AionDpsMeter.Services/PacketProcessing`.

- **Keine Verschlüsselung** in Richtung Server → Client. Nur Client → Server ist verschlüsselt, und die Richtung brauchen wir nicht.
- **Sync-Muster** `0E 00 36`: ein 11-Byte-Keepalive, an dem sich der TCP-Strom ausrichtet.
- **Rahmen:** `varint L` + Daten. Die Gesamtlänge ist `L + len(varint) − 4`. Danach kommt ein 2-Byte-Opcode (little endian).
- **Container:** Opcode `FF FF` → `u32` entpackte Größe + LZ4-Block. Der Inhalt sind wieder Rahmen. Optional steht ein Flag-Byte `0xF0–0xFE` davor.

| Opcode | Bedeutung |
|---|---|
| `0x3804` | Schaden |
| `0x3805` | Schaden über Zeit (DoT) |
| `0x3603` | Serverzeit (daraus wird der Ping berechnet) |
| `0x8D00` | Rest-HP |
| `0x8D04` | Tod einer Einheit |
| `0x3641` | Mob/Beschwörung erscheint |
| `0x382A`, `0x382B` | Buffs |
| `0x3633` | eigener Charakter |
| `0x3645` | andere Spieler |
| `0x3620` | Verknüpfung globale ↔ Session-ID |
| `0x3649` | Charakterwerte |
| `0x9702` | Gruppe |
| `0x3847` | Skill-Abklingzeit |

**Schadenspaket:**
1. `target varint`
2. `switch varint` (`&0x0F`, 4–7)
3. `flag varint`
4. `actor varint`
5. `skill u32`
6. `u8`
7. `damageType varint` (3 = kritisch)
8. bei switch ≠ 4: `flags u8, u8, direction u8`
9. `u32` und 4 Byte
10. `varint`
11. `damage varint`

Die Klasse ergibt sich aus den ersten zwei Ziffern der Skill-ID: 11 Gladiator, 12 Templer, 13 Assassine, 14 Waldläufer, 15 Magier, 16 Beschwörer, 17 Kleriker, 18 Kantor, 19 Faustkämpfer.

**Was nach Patches typischerweise bricht** (laut RATmeter-Historie):
- Der Aufbau des Gruppen-Pakets (mehrmals im Sommer 2026)
- Der Aufbau des Schadenspakets (Season 1)
- Opcodes verschieben sich selten, das ist bisher einmal passiert (Patch 6.10)
