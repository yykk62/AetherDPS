"""Baut src/gamedata.json für den Discord-Bot aus AetherDPS.Engine/GameData (eine Quelle für App, Webseite und Bot).

Aufruf aus discord-bot/:  python tools/build-data.py
"""
import json, os

ROOT = os.path.join(os.path.dirname(__file__), '..', '..', 'AetherDPS.Engine', 'GameData')
read = lambda f: json.load(open(os.path.join(ROOT, f), encoding='utf-8-sig'))

npcs = read('de/npcs.json')
npc = lambda i: (npcs.get(str(i)) or {}).get('name') or ''

servers = []
for sid, names in read('servers.json')['servers'].items():
    if len(sid) == 4 and sid[:2] in ('13', '23'):
        servers.append({'id': int(sid), 'name': names.get('de') or names.get('en'), 'faction': 'Elyos' if sid.startswith('13') else 'Asmodier'})
servers.sort(key=lambda s: s['name'].lower())

raw = read('pets/pets.json')
groups = {'verteron': 'Verteron', 'altgard': 'Altgard', 'abyss': 'Abyss'}
where = {}
for g in read('pets/regions.json')['groups']:
    for r in g['regions']:
        for p in r['pets']:
            e = where.setdefault(p, {'group': groups.get(g['id'], g['id']), 'regions': []})
            n = r['names'].get('de') or r['names'].get('en')
            if n not in e['regions']: e['regions'].append(n)
monsters = {}
for n, p in raw['monsters'].items():
    name = npc(n)
    if name and name not in monsters.setdefault(p, []): monsters[p].append(name)
pets = []
for pid, p in raw['pets'].items():
    pid = int(pid)
    pets.append({'id': pid, 'name': (p['names'].get('de') or p['names']['en']).replace('Pet: ', ''), 'category': p['category'],
                 'group': where.get(pid, {}).get('group', ''), 'regions': where.get(pid, {}).get('regions', []),
                 'monsters': monsters.get(pid, [])[:8]})
pets.sort(key=lambda p: p['name'].lower())

bosses = []
for m in read('field_bosses.json')['maps']:
    for b in m['bosses']:
        name = npc(b['npc'])
        if not name: continue
        bosses.append({'slot': b['slot'], 'name': name, 'map': m['names'].get('de') or m['names']['en'],
                       **({'respawn': b['respawn']} if b.get('respawn') else {}), **({'fixed': b['fixed']} if b.get('fixed') else {})})

out = {'_source': 'Erzeugt von discord-bot/tools/build-data.py aus AetherDPS.Engine/GameData. Pets/Feldbosse: SkeeveAN/Aion-DPS-Meter (MIT), Spieldaten (c) NCSOFT.',
       'servers': servers, 'pets': pets, 'bosses': bosses}
path = os.path.join(os.path.dirname(__file__), '..', 'src', 'gamedata.json')
json.dump(out, open(path, 'w', encoding='utf-8'), ensure_ascii=False, separators=(',', ':'))
print(f'{len(servers)} Server, {len(pets)} Pets, {len(bosses)} Feldbosse -> {os.path.getsize(path) // 1024} KB')
