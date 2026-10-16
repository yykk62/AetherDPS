"""Schutzregeln für Claude bei der Arbeit an AetherDPS (PreToolUse-Hook für Bash/PowerShell).

Betrifft nur Befehle, die Claude ausführt – nicht die App und nicht die Spieler:
1. Kein Fenster der App öffnen, solange AION2.exe läuft (Wiedergaben mit --replay sind erlaubt).
2. Mitschnitte (Spielernamen), Impressum-Daten und Geheimnisse nie committen.
3. Alles, was online geht (push, deploy, Release), immer erst nachfragen.
"""
import json
import re
import subprocess
import sys

GESCHUETZT = re.compile(
    r"AetherDPS\.Tests[\\/]Captures|\.impressum\.json|[\\/]\.secrets[\\/]|aetherdps\.env",
    re.IGNORECASE,
)

APP_START = [
    re.compile(r"\bdotnet(\.exe)?[\"']?\s+run\b.*AetherDPS\.App", re.IGNORECASE),
    re.compile(r"Start-Process\b[^;|&\n]*AetherDPS", re.IGNORECASE),
    re.compile(r"(^|[;&|\n(]|&\s)\s*(start\s+(\"\"\s+)?)?[\"']?[^\s\"';|&]*AetherDPS(\.App)?\.exe[\"']?(\s|$)", re.IGNORECASE),
]

ONLINE = [
    (re.compile(r"\bgit\b.*\bpush\b"), "git push lädt Änderungen auf GitHub hoch"),
    (re.compile(r"\bwrangler\b.*\b(deploy|publish|secret\s+put)\b"), "wrangler stellt einen Worker online"),
    (re.compile(r"\bnpm\b.*\brun\s+deploy\b"), "npm run deploy stellt die Webseite/den Worker online"),
    (re.compile(r"\bgh\s+release\s+(create|upload|edit|delete)\b"), "gh release ändert ein GitHub-Release"),
]


def antwort(entscheidung, grund):
    print(json.dumps({
        "hookSpecificOutput": {
            "hookEventName": "PreToolUse",
            "permissionDecision": entscheidung,
            "permissionDecisionReason": grund,
        }
    }))
    sys.exit(0)


def spiel_laeuft():
    try:
        out = subprocess.run(
            ["tasklist", "/FI", "IMAGENAME eq AION2.exe", "/NH"],
            capture_output=True, text=True, timeout=5,
        ).stdout
        return "aion2.exe" in out.lower()
    except Exception:
        return False


def gestagte_dateien():
    try:
        return subprocess.run(
            ["git", "diff", "--cached", "--name-only"],
            capture_output=True, text=True, timeout=10,
        ).stdout.splitlines()
    except Exception:
        return []


def main():
    try:
        daten = json.loads(sys.stdin.buffer.read().decode("utf-8") or "{}")
    except Exception:
        return
    befehl = (daten.get("tool_input") or {}).get("command") or ""
    if not befehl:
        return

    # 1. Kein App-Fenster während des Spiels
    if "--replay" not in befehl and any(p.search(befehl) for p in APP_START) and spiel_laeuft():
        antwort("deny", "AION2.exe läuft – keine AetherDPS-Fenster öffnen, solange gespielt wird "
                        "(nur Wiedergaben mit --replay und eigenem Datenordner oder dotnet test AetherDPS.UiTests).")

    # 2. Geschützte Dateien nie committen
    if re.search(r"\bgit\b.*\badd\b", befehl):
        if GESCHUETZT.search(befehl):
            antwort("deny", "Mitschnitte, Impressum-Daten und Geheimnisse werden nie committet.")
        if re.search(r"\badd\b.*\s(-f|--force)\b", befehl):
            antwort("ask", "git add --force übergeht .gitignore – könnte Mitschnitte oder Impressum-Daten mitnehmen.")
    if re.search(r"\bgit\b.*\bcommit\b", befehl):
        treffer = [d for d in gestagte_dateien() if GESCHUETZT.search("/" + d)]
        if treffer:
            antwort("deny", "Im Commit liegen geschützte Dateien: " + ", ".join(treffer[:5])
                            + ". Erst mit git restore --staged entfernen.")

    # 3. Online nur nach Rückfrage
    for muster, grund in ONLINE:
        if muster.search(befehl):
            antwort("ask", grund + " – nur nach Rückfrage beim Nutzer.")


if __name__ == "__main__":
    main()
