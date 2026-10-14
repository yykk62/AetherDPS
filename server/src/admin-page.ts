// Admin-Oberfläche als eine eigenständige Seite (keine externen Ressourcen).
// __NONCE__ wird pro Anfrage ersetzt (Content-Security-Policy).
// Hinweis: Im eingebetteten JavaScript keine Backticks und kein "${" verwenden.
// Alle Daten werden über textContent/append eingefügt, nie über innerHTML.

export const ADMIN_HTML = String.raw`<!doctype html>
<html lang="de">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<meta name="robots" content="noindex, nofollow">
<meta name="referrer" content="no-referrer">
<title>AetherDPS – Statistik</title>
<style nonce="__NONCE__">
:root { color-scheme: dark; --bg:#0f1115; --panel:#171a21; --line:#262b36; --text:#e6e8ee; --muted:#8b93a7;
  --accent:#6aa8ff; --accent2:#f2b84b; --ok:#4cc38a; --bad:#ff6b6b; }
* { box-sizing: border-box; }
body { margin:0; background:var(--bg); color:var(--text); font:15px/1.45 system-ui,-apple-system,"Segoe UI",Roboto,sans-serif; }
main { max-width:1000px; margin:0 auto; padding:16px; }
h1 { font-size:1.4rem; margin:8px 0 4px; }
h2 { font-size:1.1rem; margin:28px 0 12px; }
.muted { color:var(--muted); }
.err { color:var(--bad); }
.cards { display:grid; grid-template-columns:repeat(auto-fit,minmax(150px,1fr)); gap:12px; margin-top:16px; }
.card, .panel, .report { background:var(--panel); border:1px solid var(--line); border-radius:10px; }
.card { padding:12px 14px; }
.card .label { color:var(--muted); font-size:.8rem; }
.card .value { font-size:1.6rem; font-weight:600; font-variant-numeric:tabular-nums; }
.panel { padding:12px; overflow-x:auto; }
svg { display:block; width:100%; height:auto; }
svg .grid { stroke:var(--line); }
svg .axis { fill:var(--muted); font-size:11px; }
svg .bar { fill:var(--accent); }
svg .line { fill:none; stroke:var(--accent2); stroke-width:2; stroke-linejoin:round; }
svg .hit { fill:transparent; }
svg .hit:hover { fill:rgba(255,255,255,.06); }
.legend { display:flex; gap:16px; font-size:.8rem; color:var(--muted); margin-top:6px; }
.legend i { display:inline-block; width:10px; height:10px; border-radius:2px; margin-right:6px; vertical-align:middle; }
.legend .lg-users { background:var(--accent); }
.legend .lg-new { background:var(--accent2); }
.tables { display:grid; grid-template-columns:repeat(auto-fit,minmax(260px,1fr)); gap:12px; }
table { width:100%; border-collapse:collapse; font-variant-numeric:tabular-nums; }
th, td { text-align:left; padding:6px 8px; border-bottom:1px solid var(--line); }
th { color:var(--muted); font-weight:500; font-size:.8rem; }
.num { text-align:right; }
.filters, .actions { display:flex; gap:8px; flex-wrap:wrap; }
.filters { margin-bottom:12px; }
button { font:inherit; color:var(--text); background:#222733; border:1px solid var(--line); border-radius:8px; padding:6px 12px; cursor:pointer; }
button:hover { border-color:var(--accent); }
button:disabled { opacity:.5; cursor:default; }
button.active { background:var(--accent); color:#0b0d12; border-color:var(--accent); }
.report { padding:12px; margin-bottom:10px; }
.meta { display:flex; flex-wrap:wrap; gap:4px 12px; color:var(--muted); font-size:.85rem; align-items:center; }
.meta b { color:var(--text); }
.badge { display:inline-block; padding:1px 8px; border-radius:999px; font-size:.75rem; font-weight:600; }
.badge.crash { background:#3a1d22; color:var(--bad); }
.badge.manual { background:#1d2a3a; color:var(--accent); }
.badge.open { background:#3a3220; color:var(--accent2); }
.badge.closed { background:#1c3329; color:var(--ok); }
.msg { white-space:pre-wrap; overflow-wrap:anywhere; margin:8px 0; }
.note { font-size:.85rem; color:var(--muted); margin:0 0 8px; white-space:pre-wrap; overflow-wrap:anywhere; }
pre { background:#0b0d12; border:1px solid var(--line); border-radius:8px; padding:10px; max-height:420px; overflow:auto;
  font:12px/1.4 ui-monospace,Consolas,monospace; margin:8px 0 0; white-space:pre; }
</style>
</head>
<body>
<main>
<h1>AetherDPS – Statistik</h1>
<p id="state" class="muted">Lade …</p>

<section class="cards">
  <div class="card"><div class="label">Nutzer heute</div><div class="value" id="c-today">–</div></div>
  <div class="card"><div class="label">Nutzer gestern</div><div class="value" id="c-yest">–</div></div>
  <div class="card"><div class="label">Ø 7 Tage (ohne heute)</div><div class="value" id="c-avg">–</div></div>
  <div class="card"><div class="label">Neuinstallationen 7 Tage</div><div class="value" id="c-new">–</div></div>
</section>

<h2>Tägliche Nutzer (60 Tage, UTC)</h2>
<div class="panel">
  <div id="chart"></div>
  <div class="legend"><span><i class="lg-users"></i>Nutzer</span><span><i class="lg-new"></i>Neuinstallationen</span></div>
</div>

<h2 id="dist-title">Verteilung</h2>
<div class="tables">
  <div class="panel"><table id="t-versions"></table></div>
  <div class="panel"><table id="t-langs"></table></div>
</div>

<h2>Fehlerberichte</h2>
<div class="filters" id="filters">
  <button data-status="open" class="active">Offen</button>
  <button data-status="closed">Erledigt</button>
  <button data-status="all">Alle</button>
</div>
<div id="reports"></div>
</main>

<script nonce="__NONCE__">
"use strict";
const KEY = new URLSearchParams(location.search).get("key") || "";
const $ = (id) => document.getElementById(id);
const fmt = new Intl.NumberFormat("de-DE");
const fmt1 = new Intl.NumberFormat("de-DE", { maximumFractionDigits: 1 });
const LANG_NAMES = { de: "Deutsch", en: "Englisch", fr: "Französisch", es: "Spanisch", pt: "Portugiesisch",
  ru: "Russisch", ja: "Japanisch", ko: "Koreanisch", "zh-Hans": "Chinesisch (vereinfacht)", "zh-Hant": "Chinesisch (traditionell)" };

async function api(path, params, init) {
  const q = new URLSearchParams(Object.assign({}, params || {}, { key: KEY }));
  const r = await fetch(path + "?" + q.toString(), Object.assign({ cache: "no-store" }, init || {}));
  if (!r.ok) throw new Error("HTTP " + r.status);
  return r.status === 204 ? null : r.json();
}

function el(tag, attrs, ...kids) {
  const n = document.createElement(tag);
  for (const [k, v] of Object.entries(attrs || {})) {
    if (k === "class") n.className = v;
    else if (k.startsWith("on")) n.addEventListener(k.slice(2), v);
    else n.setAttribute(k, v);
  }
  for (const k of kids) if (k != null) n.append(k); // Strings werden zu Textknoten (sicher).
  return n;
}
function sv(tag, attrs, ...kids) {
  const n = document.createElementNS("http://www.w3.org/2000/svg", tag);
  for (const [k, v] of Object.entries(attrs || {})) n.setAttribute(k, String(v));
  for (const k of kids) if (k != null) n.append(k);
  return n;
}

const dayStr = (offset) => new Date(Date.now() + offset * 864e5).toISOString().slice(0, 10);
const fmtDay = (d) => d.slice(8, 10) + "." + d.slice(5, 7) + "." + d.slice(0, 4);
const fmtShort = (d) => d.slice(8, 10) + "." + d.slice(5, 7) + ".";
const fmtTime = (iso) => new Date(iso).toLocaleString("de-DE", { dateStyle: "medium", timeStyle: "short" });
const fmtSize = (n) => n < 1024 ? fmt.format(n) + " Zeichen" : fmt1.format(n / 1024) + " K Zeichen";
const sum = (arr, f) => arr.reduce((a, d) => a + d[f], 0);

// ---------- Statistik ----------

async function loadStats() {
  const s = await api("/admin/api/stats", { days: 60 });
  const byDay = new Map(s.daily.map((d) => [d.day, d]));
  const series = [];
  for (let i = 59; i >= 0; i--) {
    const day = dayStr(-i), d = byDay.get(day);
    series.push({ day: day, users: d ? d.users : 0, new_installs: d ? d.new_installs : 0 });
  }
  $("c-today").textContent = fmt.format(series[59].users);
  $("c-yest").textContent = fmt.format(series[58].users);
  $("c-avg").textContent = fmt1.format(sum(series.slice(52, 59), "users") / 7);
  $("c-new").textContent = fmt.format(sum(series.slice(53), "new_installs"));
  drawChart(series);

  $("dist-title").textContent = s.day
    ? "Verteilung am " + fmtDay(s.day) + (s.day === series[59].day ? " (heute)" : " (letzter Tag mit Daten)")
    : "Verteilung (noch keine Daten)";
  fillTable($("t-versions"), "Version", s.versions.map((r) => [r.version, r.users]));
  fillTable($("t-langs"), "Sprache", s.langs.map((r) => [(LANG_NAMES[r.lang] || r.lang) + " (" + r.lang + ")", r.users]));
}

function niceStep(x) {
  const p = Math.pow(10, Math.floor(Math.log10(x)));
  const n = x / p;
  return Math.max(1, (n <= 1 ? 1 : n <= 2 ? 2 : n <= 5 ? 5 : 10) * p);
}

let lastSeries = null;
function drawChart(series) {
  lastSeries = series;
  // Breite = Containerbreite, damit Schrift auf dem Handy nicht winzig skaliert wird.
  const W = Math.max(300, Math.min(960, $("chart").clientWidth || 640)), H = 240, L = 40, R = 8, T = 10, B = 24;
  const iw = W - L - R, ih = H - T - B, bw = iw / series.length;
  const max = Math.max(1, ...series.map((d) => Math.max(d.users, d.new_installs)));
  const step = niceStep(max / 4), top = Math.ceil(max / step) * step;
  const y = (v) => T + ih - (v / top) * ih;
  const svg = sv("svg", { viewBox: "0 0 " + W + " " + H, role: "img", "aria-label": "Tägliche Nutzer und Neuinstallationen, letzte 60 Tage" });

  for (let v = 0; v <= top; v += step) {
    svg.append(sv("line", { x1: L, x2: W - R, y1: y(v), y2: y(v), class: "grid" }));
    svg.append(sv("text", { x: L - 6, y: y(v) + 4, "text-anchor": "end", class: "axis" }, fmt.format(v)));
  }
  series.forEach((d, i) => {
    if (d.users > 0) svg.append(sv("rect", { x: L + i * bw + bw * 0.15, y: y(d.users), width: bw * 0.7, height: T + ih - y(d.users), class: "bar" }));
    if ((series.length - 1 - i) % 10 === 0) {
      const last = i === series.length - 1;
      svg.append(sv("text", { x: last ? W - R : L + (i + 0.5) * bw, y: H - 6, "text-anchor": last ? "end" : "middle", class: "axis" }, fmtShort(d.day)));
    }
  });
  const pts = series.map((d, i) => (L + (i + 0.5) * bw).toFixed(1) + "," + y(d.new_installs).toFixed(1)).join(" ");
  svg.append(sv("polyline", { points: pts, class: "line" }));
  series.forEach((d, i) => {
    svg.append(sv("rect", { x: L + i * bw, y: T, width: bw, height: ih, class: "hit" },
      sv("title", null, fmtDay(d.day) + ": " + fmt.format(d.users) + " Nutzer, " + fmt.format(d.new_installs) + " neu")));
  });
  $("chart").replaceChildren(svg);
}

function fillTable(table, head, rows) {
  const total = rows.reduce((a, r) => a + r[1], 0);
  const body = rows.length
    ? rows.map(([k, v]) => el("tr", null, el("td", null, k), el("td", { class: "num" }, fmt.format(v)),
        el("td", { class: "num" }, fmt1.format((v * 100) / total) + " %")))
    : [el("tr", null, el("td", { colspan: "3", class: "muted" }, "Keine Daten"))];
  table.replaceChildren(
    el("thead", null, el("tr", null, el("th", null, head), el("th", { class: "num" }, "Nutzer"), el("th", { class: "num" }, "Anteil"))),
    el("tbody", null, ...body));
}

// ---------- Fehlerberichte ----------

let filter = "open";

async function loadReports() {
  const box = $("reports");
  box.replaceChildren(el("p", { class: "muted" }, "Lade …"));
  try {
    const list = await api("/admin/api/reports", { status: filter, limit: 100 });
    box.replaceChildren(...(list.length ? list.map(renderReport) : [noReports()]));
  } catch (e) {
    box.replaceChildren(el("p", { class: "err" }, "Fehler beim Laden: " + e.message));
  }
}
const noReports = () => el("p", { class: "muted" }, "Keine Berichte.");

async function postStatus(r, body) {
  return api("/admin/api/reports/" + r.id + "/status", null,
    { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(body) });
}

function renderReport(r) {
  const card = el("article", { class: "report" });
  const statusBadge = el("span", { class: "badge" });
  const noteP = el("p", { class: "note" });
  const pre = el("pre", { hidden: "" });
  const statusBtn = el("button", null);

  function paint() {
    statusBadge.className = "badge " + r.status;
    statusBadge.textContent = r.status === "open" ? "offen" : "erledigt";
    statusBtn.textContent = r.status === "open" ? "Erledigt" : "Wieder öffnen";
    noteP.textContent = r.note ? "Notiz: " + r.note : "";
    noteP.hidden = !r.note;
  }

  statusBtn.addEventListener("click", async () => {
    statusBtn.disabled = true;
    try {
      const next = r.status === "open" ? "closed" : "open";
      await postStatus(r, { status: next });
      r.status = next;
      if (filter !== "all") {
        card.remove();
        if (!$("reports").children.length) $("reports").append(noReports());
        return;
      }
      paint();
    } catch (e) {
      alert("Fehler: " + e.message);
    } finally {
      statusBtn.disabled = false;
    }
  });

  const noteBtn = el("button", { onclick: async () => {
    const note = prompt("Notiz zu Bericht #" + r.id, r.note || "");
    if (note === null) return;
    try {
      const res = await postStatus(r, { status: r.status, note: note.slice(0, 2000) });
      r.note = res.note;
      paint();
    } catch (e) { alert("Fehler: " + e.message); }
  } }, "Notiz");

  let logBtn = null;
  if (r.has_log) {
    let loaded = false;
    const label = "Log anzeigen (" + fmtSize(r.log_size) + ")";
    logBtn = el("button", { onclick: async () => {
      if (!loaded) {
        logBtn.disabled = true;
        try {
          const full = await api("/admin/api/reports/" + r.id);
          pre.textContent = full.log || "";
          loaded = true;
        } catch (e) {
          alert("Fehler: " + e.message);
          return;
        } finally {
          logBtn.disabled = false;
        }
      }
      pre.hidden = !pre.hidden;
      logBtn.textContent = pre.hidden ? label : "Log ausblenden";
    } }, label);
  }

  card.append(
    el("div", { class: "meta" },
      el("b", null, "#" + r.id),
      el("span", { class: "badge " + r.kind }, r.kind === "crash" ? "Absturz" : "Meldung"),
      statusBadge,
      el("span", null, fmtTime(r.created_at)),
      el("span", null, "v" + r.version),
      el("span", null, r.lang),
      el("span", null, r.os)),
    r.message ? el("div", { class: "msg" }, r.message) : el("div", { class: "msg muted" }, "(keine Nachricht)"),
    noteP,
    el("div", { class: "actions" }, statusBtn, noteBtn, logBtn),
    pre);
  paint();
  return card;
}

// ---------- Start ----------

let resizeTimer = 0;
addEventListener("resize", () => {
  clearTimeout(resizeTimer);
  resizeTimer = setTimeout(() => { if (lastSeries) drawChart(lastSeries); }, 150);
});

for (const b of document.querySelectorAll("#filters button")) {
  b.addEventListener("click", () => {
    filter = b.dataset.status;
    for (const o of document.querySelectorAll("#filters button")) o.classList.toggle("active", o === b);
    loadReports();
  });
}

loadStats()
  .then(() => { $("state").textContent = "Stand: " + new Date().toLocaleString("de-DE"); })
  .catch((e) => { $("state").textContent = "Fehler beim Laden der Statistik: " + e.message; $("state").className = "err"; });
loadReports();
</script>
</body>
</html>
`;
