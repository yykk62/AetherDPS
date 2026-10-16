"""Meldet die Slash-Befehle aus src/commands.json bei Discord an (global, für Server und „App hinzufügen“)."""
import dc, json, os
APP = '1560475211114090566'
cmds = json.load(open(os.path.join(os.path.dirname(__file__), '..', 'src', 'commands.json'), encoding='utf-8'))
for c in cmds:
    c['integration_types'] = [0, 1]   # in Servern und als App beim Nutzer
    c['contexts'] = [0, 1, 2]         # Server, DM mit dem Bot, private Chats
res = dc.req('PUT', f'/applications/{APP}/commands', cmds)
print('Befehle:', ', '.join(('/' if c.get('type', 1) == 1 else '⋯ ') + c['name'] for c in res))
# Server-Einladung für andere Gilden: nur, was die Befehle brauchen
perms = str(sum(1 << b for b in (10, 11, 14, 16, 18)))  # Kanäle sehen, schreiben, Links einbetten, Verlauf, externe Emojis
dc.req('PATCH', '/applications/@me', {'integration_types_config': {
    '0': {'oauth2_install_params': {'scopes': ['applications.commands', 'bot'], 'permissions': perms}},
    '1': {'oauth2_install_params': {'scopes': ['applications.commands'], 'permissions': '0'}}}})
print('Installation: Server + Nutzer')
