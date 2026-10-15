---
title: AetherDPS einrichten
description: Herunterladen, Npcap einrichten, Spiel starten. In fünf Minuten läuft der Meter.
updated: 2026-10-15
tags: [AetherDPS, Einstieg]
---

## Herunterladen

[AetherDPS.exe von der Release-Seite laden](https://github.com/yykk62/AetherDPS/releases/latest). Es ist eine einzige Datei, keine Installation und kein .NET nötig.

- **Voraussetzung:** Windows 10 oder 11 (64 Bit) und Npcap. Npcap richtet der Meter beim ersten Start selbst ein.
- **Wohin damit?** In einen eigenen Ordner, zum Beispiel `Dokumente\AetherDPS`. Nicht in den Download-Ordner und nicht nach `C:\Programme`, dort darf sich die Datei nicht selbst aktualisieren.

## Windows-Warnung

Beim ersten Start meldet Windows „Der Computer wurde durch Windows geschützt“. Die Datei ist noch nicht digital signiert. Auf **Weitere Informationen** → **Trotzdem ausführen** klicken.

Wer sichergehen will, vergleicht die SHA-256-Prüfsumme mit der Angabe auf der Release-Seite:

```powershell
Get-FileHash AetherDPS.exe
```

## Npcap einrichten

1. AetherDPS starten.
2. Fehlt Npcap, öffnet sich die Einrichtung. Einmal auf **Npcap jetzt installieren** klicken. Der Meter lädt Npcap von der offiziellen Seite, prüft die Signatur und startet den Installer mit den richtigen Einstellungen.
3. Du bestätigst nur noch: Admin-Abfrage → **Ja**, dann **I Agree** → **Install** → **Finish**.

## Loslegen

AION 2 starten und einloggen. Das Overlay findet die Verbindung von selbst. Mit **STRG+W** startest du einen neuen Lauf, das Overlay zeigt nur deine Gruppe.

## Updates

Der Meter sieht bei jedem Start auf GitHub nach und installiert eine neue Version gleich dort. Abschaltbar unter Einstellungen → Updates. Dabei wird nur die öffentliche Release-Seite abgefragt, von dir wird nichts gesendet.
