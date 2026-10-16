import dc, json, sys
cfg=json.load(open('cfg.json',encoding='utf-8')); G=cfg['guild']; C=cfg['channels']; R=cfg['roles']; E=cfg['emojis']
cfg.setdefault('messages',{})
GOLD=0xE0A84A; VIOLET=0xC9A8F2; GREEN=0x5CC26A
DL='https://github.com/yykk62/AetherDPS/releases/latest'; WEB='https://aetherdps.com'; GH='https://github.com/yykk62/AetherDPS'
ch=lambda k:f'<#{C[k]}>'
def save(): json.dump(cfg,open('cfg.json','w',encoding='utf-8'),ensure_ascii=False,indent=1)
def img(chan,key,file):
    if key in cfg['messages']: return
    m=dc.req('POST',f'/channels/{C[chan]}/messages',{'attachments':[{'id':0,'filename':file}]},files=[(file,open('img/'+file,'rb').read(),'image/png')])
    cfg['messages'][key]=m['id']; save()
def msg(chan,key,body):
    body={'allowed_mentions':{'parse':[]},**body}
    if key in cfg['messages']:
        dc.req('PATCH',f'/channels/{C[chan]}/messages/{cfg["messages"][key]}',body); return
    m=dc.req('POST',f'/channels/{C[chan]}/messages',body); cfg['messages'][key]=m['id']; save()
link=lambda label,emoji,url:{'type':2,'style':5,'label':label,'emoji':{'name':emoji},'url':url}
links={'type':1,'components':[link('Download','⬇️',DL),link('Webseite','🌐',WEB),link('GitHub','💻',GH)]}

# ---------- #willkommen ----------
img('willkommen','w_banner','welcome.png')
msg('willkommen','w_intro',{'embeds':[{
 'color':GOLD,
 'title':'Willkommen bei AetherDPS ✨',
 'description':('Der **kostenlose, quelloffene DPS-Meter für AION 2** (EU), auf Deutsch und in 10 Sprachen.\n'
                'Kein Konto, keine Bestenliste, deine Kampfdaten bleiben auf deinem PC.\n\u200b'),
 'fields':[
  {'name':'⚔️ Live-DPS-Meter','value':'Boss mit Lebens- und Stagger-Leiste, DPS und Anteil je Spieler, Skill-Details per Klick','inline':True},
  {'name':'🏆 Läufe-Archiv','value':'Boss-Zeiten, Bestzeiten und Kampfanalyse, als Bild direkt für Discord','inline':True},
  {'name':'👥 Profil & Gruppe','value':'Kampfkraft, Itemstufe und Waffe, sobald jemand deiner Gruppe beitritt','inline':True},
  {'name':'🐾 Pet-Radar','value':'Liegt über deiner Spielkarte und zeigt die fehlenden Pets in deiner Nähe','inline':True},
  {'name':'⏱️ Timer & Feldbosse','value':'Echte Respawn-Zeiten vom Server, Risse und eigene Timer als Pills','inline':True},
  {'name':'📶 Verbindung','value':'Laggt es bei dir, beim Anbieter oder am Spielserver?','inline':True},
  {'name':'\u200b','value':(
    '### 🧭 Hier findest du alles\n'
    f'📢 {ch("ankuendigungen")}  neue Versionen von AetherDPS\n'
    f'📰 {ch("news")}  offizielle AION-2-Patchnotes, automatisch\n'
    f'📖 {ch("anleitung")}  Download, Einrichtung, Tasten, FAQ\n'
    f'💡 {ch("forum")}  Ideen, Wünsche und Fehler, mit Abstimmung\n'
    f'💬 {ch("allgemein")}  quatschen über AION 2 und den Meter')},
  {'name':'🤖 Der AetherDPS-Bot','value':(
    'Tippe in ' + ch("allgemein") + ' einfach **/** und wähle:\n'
    '`/charakter` Profil mit Kampfkraft, Ausrüstung, Skills, Daevanion · `/vergleich` zwei Chars nebeneinander\n'
    '`/events` nächste Events und Resets als Countdown · `/feldboss` Respawn-Countdown · `/pet` Fundorte\n'
    '`/ich verknüpfen` dein Charakter: Klassen-Rolle automatisch, Rechtsklick auf dich zeigt ihn\n'
    '-# Über das Profil des Bots → **App hinzufügen** gehen die Befehle in jedem Server und in DMs.')},
 ],
 'footer':{'text':'🇬🇧 English speakers welcome! Feel free to post in English anywhere.'},
}],'components':[links]})

img('willkommen','r_banner','regeln.png')
msg('willkommen','r_text',{'embeds':[{
 'color':VIOLET,
 'title':'📜 Die Regeln',
 'description':(
  '**1 · 🤝 Respekt**\nKeine Beleidigungen, Hetze oder Diskriminierung. Kritik gern, aber sachlich.\n\n'
  '**2 · 🚫 Kein Spam, keine Werbung**\nKeine fremden Server-Einladungen, Gewinnspiele oder Eigenwerbung.\n\n'
  '**3 · 🛡️ Fair bleiben**\nKeine Cheats, Bots, Account- oder Goldhandel, und keine Hilfe dabei.\n\n'
  '**4 · 🙈 Niemanden bloßstellen**\nKein Namen-Shaming anderer Spieler. Bei Screenshots aus Läufen fremde Namen bitte schwärzen.\n\n'
  '**5 · ⬇️ Downloads nur offiziell**\nAetherDPS gibt es nur auf GitHub und aetherdps.com. **Wir schicken nie Dateien per DM.** Wer dir eine „neue Version“ privat schickt, ist ein Betrüger.\n\n'
  '**6 · 💡 Ideen und Fehler ins Forum**\nSo geht nichts verloren und alle können abstimmen. Bitte keine DMs ans Team.\n\n'
  '**7 · 📘 Discord-Regeln gelten**\nDie [Community-Richtlinien von Discord](https://discord.com/guidelines) gelten auch hier.'),
 'footer':{'text':'🇬🇧 Be respectful · no spam · no cheats/RMT · official downloads only · ideas & bugs go to the forum'},
}]})

img('willkommen','ro_banner','rollen.png')
classes=[('gladiator','Gladiator'),('templar','Templer'),('assassin','Assassine'),('ranger','Waldläufer'),('sorcerer','Magier'),
         ('elementalist','Beschwörer'),('cleric','Kleriker'),('chanter','Kantor'),('brawler','Faustkämpfer')]
msg('willkommen','ro_text',{'embeds':[{
 'color':GOLD,
 'title':'🎭 Hol dir deine Rollen',
 'description':('**⚔️ Deine Klasse:** Wähle unten bis zu drei Klassen (Main und Twinks). Sie erscheinen farbig an deinem Namen.\n\n'
   '**Pings, nur wenn du willst:**\n'
   f'🔔 <@&{R["🔔 Update-Ping"]}> bei jeder neuen AetherDPS-Version\n'
   f'📰 <@&{R["📰 AION-News-Ping"]}> bei neuen AION-2-Patchnotes\n'
   f'🧪 <@&{R["🧪 Tester"]}> Testversionen vorab ausprobieren und Feedback geben\n\n'
   '*Nochmal klicken nimmt die Rolle wieder weg. Die Antwort siehst nur du.*\n'
   '💡 **Noch einfacher:** `/ich verknüpfen` mit deinem Charakter, dann setzt der Bot deine Klasse automatisch.'),
}],'components':[
 {'type':1,'components':[{'type':3,'custom_id':'class','placeholder':'⚔️ Klasse wählen …','min_values':0,'max_values':3,
   'options':[{'label':de,'value':k,'emoji':{'id':E[de],'name':'k_'+k}} for k,de in classes]}]},
 {'type':1,'components':[
   {'type':2,'style':2,'custom_id':'ping:update','label':'Updates','emoji':{'name':'🔔'}},
   {'type':2,'style':2,'custom_id':'ping:news','label':'AION-2-News','emoji':{'name':'📰'}},
   {'type':2,'style':3,'custom_id':'ping:tester','label':'Tester werden','emoji':{'name':'🧪'}}]},
]})

# ---------- #ankündigungen ----------
img('ankuendigungen','a_banner','ankuendigungen.png')
msg('ankuendigungen','a_intro',{'embeds':[{
 'color':GOLD,'title':'📢 Hier kommt jedes Update zuerst',
 'description':('Jede neue AetherDPS-Version landet hier **automatisch** mit allen Änderungen und Download-Knopf.\n'
   'Der Meter selbst aktualisiert sich beim Start von allein, du musst nichts tun.\n\n'
   f'🔔 Erwähnt werden willst du? Hol dir die Rolle in {ch("willkommen")}.\n'
   '🏰 **Für Gilden:** Mit „Folgen“ oben im Kanal kommen die Ankündigungen auch in euren Gilden-Discord.')}]})

# ---------- #aion2-news ----------
img('news','n_banner','news.png')
msg('news','n_intro',{'embeds':[{
 'color':VIOLET,'title':'📰 AION 2 News, direkt von NCSOFT',
 'description':('Patchnotes und Ankündigungen der **offiziellen EU-Seite** (aion2.plaync.com) erscheinen hier automatisch auf Deutsch, '
   'meist wenige Minuten nach der Veröffentlichung.\n\n'
   f'🛠️ **Patch-Notizen** in Gold, mit Erwähnung für <@&{R["📰 AION-News-Ping"]}>\n'
   '📣 **Ankündigungen** (Wartung, Events, Hinweise) in Lila, ohne Erwähnung\n\n'
   '🏰 **Für Gilden:** „Folgen“ oben im Kanal holt die News in euren eigenen Discord.')}]})

# ---------- #anleitung ----------
img('anleitung','g_banner','anleitung.png')
msg('anleitung','g_steps',{'embeds':[
 {'color':GOLD,'title':'⬇️ In 3 Minuten startklar','description':(
  f'**1.** [AetherDPS.exe herunterladen]({DL}). Eine einzige Datei, keine Installation, kein .NET nötig.\n'
  '**2.** In einen **eigenen Ordner** legen, z. B. `Dokumente\\AetherDPS`. Nicht in „Downloads“ oder `C:\\Programme`, sonst klappen die Auto-Updates nicht.\n'
  '**3.** Starten. Kommt die **Windows-Warnung**, auf „Weitere Informationen“ → „Trotzdem ausführen“ klicken. Die Datei ist noch nicht signiert, das ist in Arbeit.\n'
  '**4.** **Npcap** einrichten: einmal „Npcap jetzt installieren“, dann Ja → I Agree → Install → Finish.\n'
  '**5.** AION 2 starten, einloggen, **STRG+W** drücken. Fertig! 🎉\n\n'
  '💻 Windows 10 oder 11 (64 Bit) · Updates kommen automatisch')},
 {'color':VIOLET,'title':'⌨️ Die wichtigsten Tasten','fields':[
  {'name':'STRG + W','value':'Meter starten bzw. ein- und ausblenden','inline':True},
  {'name':'STRG + S','value':'Spielbild sofort auf dem zweiten Monitor','inline':True},
  {'name':'🔒 Schloss','value':'Klicks gehen durch den Meter ins Spiel','inline':True},
  {'name':'⚙️ Automatisch erfassen','value':'Der Meter klappt bei jedem Bosskampf von selbst auf','inline':True},
  {'name':'☰ / Spieler anklicken','value':'Skill-Details für jeden Spieler','inline':True},
  {'name':'🩺 Problem melden','value':'Hauptfenster oder Tray-Symbol, kein Konto nötig','inline':True}]},
 {'color':GREEN,'title':'❓ Häufige Fragen','description':(
  '**Ist das erlaubt?**\nDer Meter liest den Netzwerkverkehr nur **passiv** mit. Er verändert nichts am Spiel, liest keinen Spielspeicher und sendet nichts ans Spiel. '
  'Er ist ein inoffizielles Fan-Projekt und nicht mit NCSOFT verbunden. Die Nutzung erfolgt auf eigene Verantwortung.\n\n'
  '**Was kostet das?**\nNichts. Der Quellcode ist offen (GPL-3.0).\n\n'
  '**Werden meine Daten hochgeladen?**\nKampfdaten und Läufe nie. Kein Konto, keine Bestenliste. Was überhaupt rausgeht, steht genau im README auf GitHub.\n\n'
  '**Nach einem Patch zeigt der Meter nichts mehr?**\nDer Meter merkt das und sagt Bescheid. Die angepasste Version kommt meist schnell und installiert sich selbst. '
  f'Aktuelle Infos dazu kommen in {ch("ankuendigungen")}.')},
 {'color':0xE2554F,'title':'🆘 Etwas klappt nicht?','description':(
  '**1.** In der App: **„Problem melden“**. Das geht am schnellsten, Namen werden vorher entfernt.\n'
  f'**2.** Oder einen Beitrag in {ch("forum")} mit dem Tag 🐞 **Fehler**, mit Version und kurzer Beschreibung.\n'
  f'**3.** Oder ein [Issue auf GitHub]({GH}/issues).')},
],'components':[{'type':1,'components':[link('Download','⬇️',DL),link('Ausführliche Anleitung','📘',GH+'#readme'),link('Webseite','🌐',WEB)]}]})

# ---------- Forum: angepinnter Leitfaden ----------
if 'forum_guide' not in cfg['messages']:
    thread=dc.req('POST',f'/channels/{C["forum"]}/threads',{'name':'📌 So funktioniert dieses Forum','applied_tags':[cfg['forum_tags']['Frage']],
     'message':{'attachments':[{'id':0,'filename':'feedback.png'}],'allowed_mentions':{'parse':[]},'embeds':[{'color':GOLD,'title':'💡 Deine Ideen machen AetherDPS besser',
      'description':('Jede Idee und jeder Fehler bekommt einen **eigenen Beitrag**. So lässt sich darüber reden, abstimmen und der Stand verfolgen.\n\n'
       '**So geht’s**\n'
       '**1.** Erst kurz suchen, ob es das schon gibt. Wenn ja, dort mit 👍 abstimmen statt neu posten.\n'
       '**2.** „Neuer Beitrag“, aussagekräftiger Titel, einen Tag wählen: 💡 Idee · ✨ Verbesserung · 🐞 Fehler · ❓ Frage\n'
       '**3.** Bei Fehlern: AetherDPS-Version, was du gemacht hast und was passiert ist. Am besten zusätzlich „Problem melden“ in der App.\n\n'
       '**Was aus deinem Beitrag wird**\n'
       '👀 Wird geprüft → 📌 Geplant → 🛠️ In Arbeit → ✅ Umgesetzt\n'
       '🟢 Gelöst (Fehler behoben, Frage beantwortet) · 🚫 Abgelehnt (mit Begründung)\n\n'
       '**Ganz neue Features ausdrücklich erwünscht!** Je mehr 👍 eine Idee hat, desto eher kommt sie dran.'),
      'image':{'url':'attachment://feedback.png'},'footer':{'text':'🇬🇧 English posts are welcome too'}}]}},
     files=[('feedback.png',open('img/feedback.png','rb').read(),'image/png')])
    dc.req('PATCH',f'/channels/{thread["id"]}',{'flags':2,'locked':True})
    cfg['messages']['forum_guide']=thread['id']; save()

# ---------- #allgemein ----------
msg('allgemein','chat_hello',{'content':('👋 **Willkommen im Chat!** Hier geht’s um alles rund um AION 2 und AetherDPS: Builds, Dungeons, Läufe, Fragen.\n'
  f'Für Ideen und Fehler gibt es {ch("forum")}, da gehen sie nicht unter. 🇬🇧 English is fine too!')})
save()
print('fertig', len(cfg['messages']))
