# Changes

Newest versions first.

## 0.7.9

- **Tidier pet radar:** no title bar any more – the buttons sit small on the map (⠿ move, ▾ collapse, ✕ close, ☰ list, ⌖ to you)
- **List fully hidden:** ☰ hides the missing pets completely instead of leaving an empty bar; ☰ again brings them back

## 0.7.8

- Pets tab: **all** monsters of a pet are always shown above the map (before, anything past the second row was scrolled out of view)

## 0.7.7

- **Pet radar as big as the game map:** the first time it opens centred right over the game map and can replace it. Drag the bottom-right corner to resize; position and size are remembered
- **Collapse:** ▾ shrinks the radar to a tiny bar, one click opens it again
- **Which monster?** In the radar list each pet shows the monster at the nearest spawn – the soul often has a different name (Aberrant Bee drops from the Black-patterned Bee, for example)
- **Pets tab with buttons instead of dropdowns:** map (only your side and the Abyss), species as pictures in their colour, status as "All · Missing · Not max". The monsters are shown more compactly above the map

## 0.7.6

- **Pets tab redesigned:** everything fits in the window, no more scrolling. The pet radar setting is at the top, the list and map fill the rest. The monsters that drop the soul are shown as small chips above the map
- **Note in the profile:** NCSOFT shows skills (and possibly gear) from slot 1 – if you play with another slot, values here may differ

## 0.7.5

- **Pet radar as a minimap:** you are always in the centre, the map follows you – even while flying – and remembers your zoom (also after a restart). The mouse wheel zooms around you; dragging lets you look around, after 5 seconds the map snaps back to you (double-click or ⌖ right away)
- **Clicking a pet no longer zooms far out:** your zoom stays. Only if no spawn point is in view does the map zoom out just enough to show the nearest one
- **Buff and cooldown bar below the meter removed** – it was too imprecise and too busy. Character detection and the buff analysis in runs stay
- Field boss data rebuilt (slots by our own rule, the meter learns respawn times itself), credits tidied up

## 0.7.4

- **Colours by species:** every pet species has a fixed colour – Cogni blue, Fera orange, Natura green, Varian purple, Special yellow – in the list, on the map in the Pets tab and in the radar
- **Smarter radar:** without a species filter it shows the nearest missing pet **per species** in that colour. If you pick a species in the Pets tab, it shows the five nearest of that species in different colours. The chosen species is remembered

## 0.7.3

- **Your position on the map:** the meter now reads it from every effect you cast (attacks, buffs) – about 40 times more often than before. The dot shows up as soon as you fight or buff
- **Pet radar tidied up:** the map only shows the five nearest pets you still need, each in its own colour (the dot on the picture in the list shows which). The list scrolls and shows up to 30 pets
- **⌖ Center** like Google Maps, in the radar and the Pets tab. The radar follows you; if you pan yourself it stays put until you press ⌖
- **Map in the Pets tab:** always shows the whole map or your own view – picking another pet only sets the markers, without zooming. Double-click: whole map

## 0.7.2

- **Pet radar fixed:** it did not open when the meter was started after logging in (e.g. after an update). Now opening the map is enough
- **Pet radar "always":** besides "while the game map is open" the radar can stay on screen and follow you (Pets tab)
- **Pets you need in the radar:** a collapsible list of the nearest pets you still need (by your filter in the Pets tab) below the map. Click a pet to see all its spawn points, zoomed out – so you know where to teleport
- **Pictures for all 207 pets** in the list, details and radar
- **Map in the Pets tab:** bigger, and dragging works as expected (only while the left button is held – before, the map could stick to the cursor)
- The filter in the Pets tab is remembered and also applies to the radar

## 0.7.1

- **Timers as single pills:** the timer box is gone. Every timer is its own pill of the same size that you can drag anywhere. Place several roughly side by side, stacked or as a block and right-click **Group**: the arrangement is kept (1×3, 3×1, 2×2 …) and the group moves together. Right-click also to **take out**, **ungroup**, set **columns**, **remove** a pill (without opening the app) and **lock** (unlock in the Timers tab)
- **Pet radar:** when you open the map in game, a small window at the left edge shows the spawn points of the pets you still need around your position, plus the nearest ones with distance and direction. Movable, can be turned off in the Pets tab
- **Pet finder:** filters "not collected yet" and "not at max level" instead of the single checkbox
- Profile: stigma skills are now called that

## 0.7.0

- **New Profile tab:** your character at a glance, even without the game running – combat power, item level, average enchant, gear (click an item for its stats, manastones and godstone), all stats with their effects, Daevanion progress per board, titles, pet, wings and skills. Look up any other character on an EU server by name
- **New in your party:** when someone joins your party, their card appears at the right edge with combat power, item level, enchant, Daevanion and weapon. Click opens the full profile. Can be turned off in the Party tab
- **Pet finder (Pets tab):** every pet of Verteron, Altgard and the Abyss with your level and progress (read when you log in) and a map with all spawn points of its monsters, their areas and your position. Filter by species, search for pet or monster, "only unfinished"

## 0.6.4

- **Character profiles in the Party tab:** click yourself ("My character") or a party member to see level, item level, combat power, full gear with enchantments and skills – straight from NCSOFT (the same public data as on aion2.plaync.com). Only requested when you click

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
