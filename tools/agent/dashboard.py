#!/usr/bin/env python3
"""Generate the Hollowfen production dashboard (static HTML) from repo state.

Sources: TODOS.md (queue + snapshot), QUESTIONS.md (decision inbox),
Hollowfen-Unity/Docs/worksheets/batch-*.md (shipped work, decisions,
verification evidence), and git log/tags.

Usage:
  python3 tools/agent/dashboard.py [--output /path/to/dashboard.html]

Run from anywhere; the repo root is derived from this file's location.
The night-shift wrap-up regenerates this and republishes the Artifact so
Trevor's morning board is always current. Stdlib only.
"""

import argparse
import datetime
import html
import os
import re
import subprocess

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
WORKSHEETS = os.path.join(ROOT, "Hollowfen-Unity", "Docs", "worksheets")


def read(path):
    try:
        with open(path, encoding="utf-8") as f:
            return f.read()
    except OSError:
        return ""


def esc(s):
    return html.escape(s, quote=True)


def inline_md(s):
    """Escape HTML, then render the two inline markdown forms the docs use."""
    s = esc(s)
    s = re.sub(r"`([^`]+)`", r"<code>\1</code>", s)
    s = re.sub(r"\*\*([^*]+)\*\*", r"<strong>\1</strong>", s)
    s = re.sub(r"~~([^~]+)~~", r"<s>\1</s>", s)
    return s


def git(*args):
    try:
        out = subprocess.run(
            ["git", "-C", ROOT] + list(args), capture_output=True, text=True, timeout=15
        )
        return out.stdout.strip()
    except Exception:
        return ""


# ---------- parsers ----------

def parse_todos():
    text = read(os.path.join(ROOT, "TODOS.md"))
    snapshot = ""
    m = re.search(r"\*\*Status snapshot[^*]*\*\*(.+?)(?:\n\n|\n---)", text, re.S)
    if m:
        snapshot = " ".join(m.group(1).split())
    items = []
    m = re.search(r"## Next up.*?\n(.*?)\n## ", text, re.S)
    if m:
        for line in m.group(1).splitlines():
            im = re.match(r"^(\d+)\.\s+(.*)", line.strip())
            if im:
                body = im.group(2)
                done = body.startswith("~~") or "**DONE" in body
                title = re.split(r" — |\.(?:\s|$)", body)[0]
                items.append({"n": im.group(1), "title": title, "done": done})
    return snapshot, items


def parse_questions():
    text = read(os.path.join(ROOT, "QUESTIONS.md"))
    open_block = re.search(r"## Open\n(.*?)(?:\n## |\Z)", text, re.S)
    questions = []
    if open_block:
        parts = re.split(r"\n### ", "\n" + open_block.group(1))
        for p in parts:
            p = p.strip()
            if not p or p.startswith("*("):
                continue
            lines = p.splitlines()
            questions.append({"title": lines[0].strip(), "body": "\n".join(lines[1:]).strip()})
    return questions


def parse_worksheets():
    sheets = []
    if not os.path.isdir(WORKSHEETS):
        return sheets
    for name in sorted(os.listdir(WORKSHEETS), reverse=True):
        if not name.startswith("batch-") or not name.endswith(".md"):
            continue
        text = read(os.path.join(WORKSHEETS, name))
        title = ""
        m = re.search(r"^# (.+)$", text, re.M)
        if m:
            title = m.group(1)
        meta = ""
        m = re.search(r"\*\*Date:\*\*(.+)", text)
        if m:
            meta = " ".join(("Date:" + m.group(1)).replace("**", "").split())
        goal = ""
        m = re.search(r"## Goal\n(.+?)(?:\n\n|\n## )", text, re.S)
        if m:
            goal = " ".join(m.group(1).split())
        decisions = []
        m = re.search(r"## Decisions made\n(.*?)(?:\n## |\Z)", text, re.S)
        if m:
            for row in m.group(1).splitlines():
                cells = [c.strip() for c in row.strip().strip("|").split("|")]
                if len(cells) >= 3 and cells[0] not in ("Decision", "---", ""):
                    if set(cells[0]) != {"-"}:
                        decisions.append(cells[:3])
        verify = []
        m = re.search(r"## Verification evidence\n(.*?)(?:\n## |\Z)", text, re.S)
        if m:
            verify = [l.strip("- ").strip() for l in m.group(1).splitlines() if l.strip().startswith("-")]
        feedback = []
        m = re.search(r"## Feedback to Trevor\n(.*?)(?:\n## |\Z)", text, re.S)
        if m:
            feedback = [l.strip("- ").strip() for l in m.group(1).splitlines() if l.strip().startswith("-")]
        verified = bool(re.search(r"verified|0 console errors", " ".join(verify), re.I))
        committed = "committed" in meta.lower()
        sheets.append({
            "file": name, "title": title, "meta": meta, "goal": goal,
            "decisions": decisions, "verify": verify, "feedback": feedback,
            "verified": verified, "committed": committed,
        })
    return sheets


def parse_models():
    """Read the Meshy wants list (asset-dropoff.md) into priority groups of
    checklist items so Trevor can see which models still need making."""
    text = read(os.path.join(ROOT, "Hollowfen-Unity", "Docs", "asset-dropoff.md"))
    m = re.search(r"## Wants list.*?\n(.*)", text, re.S)
    groups, todo, done = [], 0, 0
    if not m:
        return groups, todo, done
    cur = None
    for line in m.group(1).splitlines():
        h = re.match(r"^### (.+)", line.strip())
        if h:
            cur = {"label": h.group(1).strip(), "items": []}
            groups.append(cur)
            continue
        im = re.match(r"^- \[([ xX])\]\s+(.*)", line.strip())
        if im and cur is not None:
            is_done = im.group(1).lower() == "x"
            cur["items"].append({"text": im.group(2).strip(), "done": is_done})
            todo += 0 if is_done else 1
            done += 1 if is_done else 0
    groups = [g for g in groups if g["items"]]  # drop free-text sections
    return groups, todo, done


# ---------- render ----------

CSS = """
:root{
  --paper:#f2ecdd; --card:#faf6ea; --ink:#2b2114; --ink-soft:#5d5340;
  --gold:#a67f2e; --gold-soft:#c9a959; --moss:#5c6b4a; --alert:#a8402f;
  --rule:#d8cdb2; --chip-ink:#f5efdf; --mono-bg:#e9e1cb;
}
@media (prefers-color-scheme: dark){:root{
  --paper:#1c1712; --card:#262019; --ink:#ede4d0; --ink-soft:#a89a80;
  --gold:#c9a959; --gold-soft:#a67f2e; --moss:#8fa377; --alert:#d9705c;
  --rule:#3d342a; --chip-ink:#1c1712; --mono-bg:#141109;
}}
:root[data-theme="dark"]{
  --paper:#1c1712; --card:#262019; --ink:#ede4d0; --ink-soft:#a89a80;
  --gold:#c9a959; --gold-soft:#a67f2e; --moss:#8fa377; --alert:#d9705c;
  --rule:#3d342a; --chip-ink:#1c1712; --mono-bg:#141109;
}
:root[data-theme="light"]{
  --paper:#f2ecdd; --card:#faf6ea; --ink:#2b2114; --ink-soft:#5d5340;
  --gold:#a67f2e; --gold-soft:#c9a959; --moss:#5c6b4a; --alert:#a8402f;
  --rule:#d8cdb2; --chip-ink:#f5efdf; --mono-bg:#e9e1cb;
}
*{box-sizing:border-box}
body{background:var(--paper);color:var(--ink);margin:0;
  font:15px/1.55 -apple-system,BlinkMacSystemFont,"Segoe UI",Roboto,sans-serif}
.wrap{max-width:1020px;margin:0 auto;padding:36px 22px 60px;display:flex;flex-direction:column;gap:30px}
.eyebrow{font-size:11px;font-weight:700;letter-spacing:.28em;color:var(--gold);text-transform:uppercase}
h1{font-family:Georgia,"Times New Roman",serif;font-size:36px;margin:4px 0 2px;font-weight:400;text-wrap:balance}
h2{font-family:Georgia,serif;font-size:21px;font-weight:400;margin:0}
.sub{color:var(--ink-soft);max-width:72ch}
.rule{height:1px;background:linear-gradient(90deg,var(--gold),transparent);border:0;margin:14px 0 0}
.stats{display:flex;gap:12px;flex-wrap:wrap}
.stat{background:var(--card);border:1px solid var(--rule);border-radius:6px;padding:12px 18px;min-width:130px}
.stat b{display:block;font-size:24px;font-family:Georgia,serif;font-weight:400;font-variant-numeric:tabular-nums}
.stat span{font-size:11px;letter-spacing:.14em;text-transform:uppercase;color:var(--ink-soft)}
section{display:flex;flex-direction:column;gap:12px}
.sec-head{display:flex;align-items:baseline;gap:10px;border-bottom:1px solid var(--rule);padding-bottom:8px}
.count{font-size:12px;color:var(--ink-soft);font-variant-numeric:tabular-nums}
.card{background:var(--card);border:1px solid var(--rule);border-radius:6px;padding:16px 18px}
.q{border-left:3px solid var(--alert)}
.q h3{font-family:Georgia,serif;font-weight:400;font-size:17px;margin:0 0 6px}
.q .body{color:var(--ink-soft);white-space:pre-line;font-size:14px}
.chip{display:inline-block;font-size:10.5px;font-weight:700;letter-spacing:.12em;text-transform:uppercase;
  padding:2px 9px;border-radius:99px;vertical-align:2px}
.chip.needs{background:var(--alert);color:var(--chip-ink)}
.chip.ok{background:var(--moss);color:var(--chip-ink)}
.chip.tag{background:var(--gold);color:var(--chip-ink)}
.ship{display:flex;flex-direction:column;gap:8px}
.ship .head{display:flex;gap:10px;align-items:baseline;flex-wrap:wrap}
.ship .meta{font-size:12.5px;color:var(--ink-soft)}
.ship .goal{font-size:14px;color:var(--ink-soft);max-width:78ch}
details{font-size:13.5px}
summary{cursor:pointer;color:var(--gold);font-size:12.5px;letter-spacing:.06em}
details ul{margin:8px 0 0;padding-left:18px;color:var(--ink-soft)}
table{width:100%;border-collapse:collapse;font-size:13.5px}
th{text-align:left;font-size:10.5px;letter-spacing:.14em;text-transform:uppercase;color:var(--ink-soft);
  border-bottom:1px solid var(--rule);padding:6px 10px 6px 0;font-weight:700}
td{padding:8px 10px 8px 0;border-bottom:1px solid var(--rule);vertical-align:top}
.table-scroll{overflow-x:auto}
ol.queue{margin:0;padding-left:0;list-style:none;counter-reset:q}
ol.queue li{counter-increment:q;display:flex;gap:12px;padding:9px 0;border-bottom:1px solid var(--rule);align-items:baseline}
ol.queue li::before{content:counter(q,decimal-leading-zero);font-family:Georgia,serif;color:var(--gold);
  font-variant-numeric:tabular-nums;font-size:15px;min-width:26px}
ol.queue li.done{opacity:.5}ol.queue li.done .t{text-decoration:line-through}
.commits{font:12.5px/1.9 ui-monospace,SFMono-Regular,Menlo,monospace;background:var(--mono-bg);
  border:1px solid var(--rule);border-radius:6px;padding:12px 16px;overflow-x:auto;white-space:pre}
code{font:.92em ui-monospace,SFMono-Regular,Menlo,monospace;background:var(--mono-bg);
  padding:1px 5px;border-radius:4px}
footer{color:var(--ink-soft);font-size:12px;border-top:1px solid var(--rule);padding-top:14px}
.mgroup{margin-bottom:14px}
.mgroup:last-child{margin-bottom:0}
.mghead{display:flex;align-items:baseline;gap:10px;font-weight:700;font-size:13px;
  letter-spacing:.02em;color:var(--ink);margin-bottom:6px}
.mlist{list-style:none;margin:0;padding:0}
.mlist li{display:flex;gap:9px;align-items:baseline;padding:4px 0;font-size:13.5px}
.mlist .box{font-size:14px;line-height:1;color:var(--gold)}
.mlist li.done{opacity:.5}.mlist li.done .box{color:var(--moss)}.mlist li.done .t{text-decoration:line-through}
@media (max-width:560px){h1{font-size:28px}.stat{min-width:44%}}
"""


def build_html():
    snapshot, queue = parse_todos()
    questions = parse_questions()
    sheets = parse_worksheets()
    model_groups, models_todo, models_done = parse_models()
    commits = git("log", "--format=%h  %d %s", "-10")
    tags = [t for t in git("tag").splitlines() if t]
    dirty = git("status", "--porcelain")
    now = datetime.datetime.now().strftime("%A, %B %-d %Y · %H:%M")

    q_html = "".join(
        f'<div class="card q"><span class="chip needs">Needs Trevor</span>'
        f"<h3>{inline_md(q['title'])}</h3>"
        f"<div class=\"body\">{inline_md(q['body'])}</div></div>"
        for q in questions
    ) or '<div class="card"><em>Inbox zero — nothing waiting on you.</em></div>'

    ships = []
    for s in sheets:
        chips = ""
        m = re.match(r"batch-\d+", s["file"])
        if m:
            chips += f'<span class="chip tag">{esc(m.group(0))}</span>'
        if s["verified"]:
            chips += '<span class="chip ok">verified</span>'
        ver = ""
        if s["verify"]:
            ver = ("<details><summary>Verification evidence</summary><ul>"
                   + "".join(f"<li>{inline_md(v)}</li>" for v in s["verify"]) + "</ul></details>")
        fb = ""
        if s["feedback"]:
            fb = ("<details><summary>Agent feedback</summary><ul>"
                  + "".join(f"<li>{inline_md(v)}</li>" for v in s["feedback"]) + "</ul></details>")
        ships.append(
            f'<div class="card ship"><div class="head">{chips}<h2>{inline_md(s["title"])}</h2></div>'
            f'<div class="meta">{inline_md(s["meta"])}</div>'
            f'<div class="goal">{inline_md(s["goal"])}</div>{ver}{fb}</div>'
        )

    rows = ""
    for s in sheets:
        m = re.match(r"batch-\d+", s["file"])
        batch = m.group(0) if m else s["file"]
        for d in s["decisions"]:
            rows += (f"<tr><td><code>{esc(batch)}</code></td><td>{inline_md(d[0])}</td>"
                     f"<td>{inline_md(d[1])}</td><td>{inline_md(d[2])}</td></tr>")
    decisions_html = (
        f'<div class="card table-scroll"><table><tr><th>Batch</th><th>Decision</th>'
        f"<th>Choice</th><th>Why</th></tr>{rows}</table></div>"
    ) if rows else ""

    queue_html = "".join(
        f'<li class="{"done" if i["done"] else ""}"><span class="t">{inline_md(i["title"].strip("~"))}</span></li>'
        for i in queue
    )

    mg_html = ""
    for g in model_groups:
        items = "".join(
            f'<li class="mitem {"done" if it["done"] else "todo"}">'
            f'<span class="box">{"&#9745;" if it["done"] else "&#9744;"}</span>'
            f'<span class="t">{inline_md(it["text"])}</span></li>'
            for it in g["items"]
        )
        n_todo = sum(1 for it in g["items"] if not it["done"])
        mg_html += (
            f'<div class="mgroup"><div class="mghead">{inline_md(g["label"])}'
            f'<span class="count">{n_todo} to make</span></div>'
            f'<ul class="mlist">{items}</ul></div>'
        )
    models_html = (
        f'<div class="card">{mg_html}</div>' if mg_html
        else '<div class="card"><em>All catalogued models delivered.</em></div>'
    )

    tree = "clean" if not dirty else f"{len(dirty.splitlines())} uncommitted files"

    return f"""<meta charset="utf-8">
<title>Hollowfen — Production Board</title>
<style>{CSS}</style>
<div class="wrap">
<header>
  <div class="eyebrow">Field Journal · Operations</div>
  <h1>Hollowfen — Production Board</h1>
  <div class="sub">{inline_md(snapshot)}</div>
  <div class="sub" style="font-size:12.5px;margin-top:4px">Generated {esc(now)} · working tree {esc(tree)}</div>
  <hr class="rule">
</header>
<div class="stats">
  <div class="stat"><b>{len(questions)}</b><span>open questions</span></div>
  <div class="stat"><b>{len(tags)}</b><span>tagged batches</span></div>
  <div class="stat"><b>{sum(1 for i in queue if not i["done"])}</b><span>items queued</span></div>
  <div class="stat"><b>{models_todo}</b><span>models to make</span></div>
</div>
<section>
  <div class="sec-head"><h2>Waiting on you</h2><span class="count">{len(questions)} open · QUESTIONS.md</span></div>
  {q_html}
</section>
<section>
  <div class="sec-head"><h2>Shipped</h2><span class="count">newest first · Docs/worksheets/</span></div>
  {"".join(ships)}
</section>
{f'<section><div class="sec-head"><h2>Decision log</h2></div>{decisions_html}</section>' if decisions_html else ""}
<section>
  <div class="sec-head"><h2>Up next</h2><span class="count">TODOS.md · agents pull from the top</span></div>
  <div class="card"><ol class="queue">{queue_html}</ol></div>
</section>
<section>
  <div class="sec-head"><h2>Models to make</h2><span class="count">{models_todo} open · Meshy · Docs/asset-dropoff.md</span></div>
  {models_html}
</section>
<section>
  <div class="sec-head"><h2>Recent commits</h2></div>
  <div class="commits">{esc(commits)}</div>
</section>
<footer>Regenerate: <code>python3 tools/agent/dashboard.py</code> → republish Artifact. Sources: TODOS.md · QUESTIONS.md · Docs/worksheets/ · git.</footer>
</div>"""


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--output", default=os.path.join(ROOT, "Hollowfen-Unity", "Docs", "dashboard.html"))
    args = ap.parse_args()
    html_out = build_html()
    with open(args.output, "w", encoding="utf-8") as f:
        f.write(html_out)
    print(f"wrote {args.output} ({len(html_out)} bytes)")


if __name__ == "__main__":
    main()
