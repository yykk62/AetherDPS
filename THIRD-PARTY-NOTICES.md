# Fremde Quellen / Third-party notices

AetherDPS ist GPL-3.0 (siehe LICENSE). Daten und Erkenntnisse aus diesen Projekten sind eingeflossen:

| Projekt | Lizenz | Was |
|---|---|---|
| [RATmeter](https://github.com/Kuroukihime/AIon2-Dps-Meter) (Kuroukihime) | GPL-3.0 | Grundlage (Fork): Mitschnitt, Paketverarbeitung, Kampf-Sessions, Skill-/Mob-Daten |
| [A2Tools](https://github.com/taengu/A2Tools-DPS-Meter) (taengu) | GPL-3.0 | Spieltexte in 10 Sprachen, Servernamen, Liste der offenen Welt, Kartenwechsel-Paket; die Buff-Daten darin stammen aus [Daevalog](https://github.com/Seralth/Daevalog) (Seralth, GPL-3.0) |
| [Aion-DPS-Meter](https://github.com/SkeeveAN/Aion-DPS-Meter) (SkeeveTV) | MIT | Feldbosse je Karte mit Koordinaten, Kartennamen, Abyss-Zeitpläne (`GameData/field_bosses.json`); Pets, Spawnpunkte, Gebiete, Karten-Kalibrierung (`GameData/pets/`, `tools/update-pet-data.py`), Weltkarten-Kacheln (`Assets/maps/`, verkleinert), Aufbau von Pet-Liste (0x9000) und Positions-Paket (0x373C) |

Hinweise auf Paket-Aufbau und Spielabläufe (ohne übernommenen Code oder Daten) kamen außerdem aus aion2dps, aion2-dps-meter, waffle_meter, aion2-api und AionDpsMeter.

Spieldaten, Namen, Weltkarten, Pet-Porträts (`Assets/pets/`, einmalig von assets.playnccdn.com übernommen) und Klassen-Icons: © NCSOFT. AION ist eine Marke von NCSOFT. AetherDPS ist ein inoffizielles Fan-Projekt.

## MIT License – Aion-DPS-Meter

Copyright (c) 2026 SkeeveTV

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
