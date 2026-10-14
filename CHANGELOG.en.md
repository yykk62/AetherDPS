# Changes

Newest versions first.

## 0.6.3

- **Timers as tiles:** every timer in the overlay is a small tile (name on top, time large). Right-click the header: **columns** (1–4, e.g. 2×2 or 3×2), **detach all** or **attach all**. Drag single tiles anywhere – drop them on the group to put them back
- **Overlay only shortly before:** besides "always" and "off" there is "only show shortly before" (default 5 minutes). Reminders come either way
- **Fixed events instead of the schedule editor:** Space-Time Rift and Artifact Siege are built in and can only be switched on or off. Use custom timers for everything else
- **Field bosses of your own faction only:** Elyos see Verteron, Asmodians Altgard, both see the Abyss

## 0.6.2

- **Fixed:** if the meter started before the game, or while Steam was downloading an update, it could lock onto the wrong network traffic and then showed nothing at all (no field bosses either). It now finds the game server reliably and searches again by itself if nothing comes from the game

## 0.6.1

- **Fixed:** after updating from 0.5.0 the meter no longer started (it cleaned up its own unpacked files on start). If this hit you: [download AetherDPS.exe again](https://github.com/yykk62/AetherDPS/releases/latest) – your settings and runs are kept

## 0.6.0

- **New name: AetherDPS.** Settings, runs and timers carry over on first start, updates keep working as before
- **Dungeons by name:** the meter knows which dungeon you are in (including difficulty). Runs are named after the dungeon, and the mini bar shows it
- **10 languages:** German, English, French, Spanish, Portuguese, Russian, Japanese, Korean and Chinese (Simplified and Traditional) for the interface and for skills, monsters, classes, dungeons and servers (Settings → Language, default: same as Windows)
- **All EU server names** instead of numbers
- **Report a problem** sends your description and, if you like, the log straight to the developer. No account, no email. Player names are removed first
- **Crash reports:** if the meter crashed, it asks on the next start whether it may send a report
- **Game update detected:** if the meter no longer understands the game data after a patch, it tells you instead of staying empty
- **Messages from the developer** (e.g. "update coming tonight") show up in the main window
- **Anonymous usage count** (opt-in, asked on start): one signal per day with version and language, no names, no ID
- "What's new" after every update
- **Field bosses with the server's real times:** open the map in game and the meter knows every field boss on it – who is alive and when the others respawn (Verteron, Altgard, Abyss). Star a boss to see it in the timer overlay and get a reminder before it spawns. The meter learns respawn durations as it goes
- **Dungeon time limit** as a countdown on the boss card and mini bar (from the server, red in the last minute)
- **Capture rate:** for defeated bosses the run archive shows how much of their HP the meter captured – so you know nothing is missing
- **Damage taken** per player in the combat analysis
- **Post to Discord:** add a webhook in the settings, and one click posts the run as image and text to your channel
- **EU schedule:** Artifact Siege (Mon, Thu, Sat 21:00) instead of the old estimated templates; Abyss bosses are now listed with the field bosses
