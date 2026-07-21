#!/usr/bin/env python3
"""Hollowfen gotcha linter — scans OUR code and content (Assets/_Hollowfen)
for the specific failure modes documented in Docs/conventions.md. Runs from
the pre-commit hook; no Unity required.

Rules (severity):
  legacy-input   ERROR  UnityEngine.Input.* / Input.GetKey|GetAxis|... (new Input System only)
  datapath-save  ERROR  Application.dataPath (saves/writes must use persistentDataPath)
  emoji          ERROR  emoji codepoints in dialogue/story-card .asset text
  missing-meta   ERROR  file or folder without a sibling .meta (or orphaned .meta)
  public-field   WARN   public mutable field on a MonoBehaviour/ScriptableObject class
                        (prefer [SerializeField] private / properties). Plain data
                        classes, DTOs, and serializable structs are the accepted
                        exception and are not flagged.

Waivers: tools/agent/lint_waivers.txt, lines of "<path-substring>::<rule>  # reason".
Exit codes: 0 clean, 1 unwaived ERRORs. New gotcha class discovered → add a rule
here AND a line in Docs/conventions.md.
"""

import os
import re
import sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SCRIPTS = os.path.join(REPO, "Hollowfen-Unity", "Assets", "_Hollowfen", "Scripts")
ASSETS_ROOT = os.path.join(REPO, "Hollowfen-Unity", "Assets", "_Hollowfen")
CONTENT_DIRS = [
    os.path.join(ASSETS_ROOT, "Data", "Dialogue"),
    os.path.join(ASSETS_ROOT, "Data", "StoryCards"),
]
WAIVERS_FILE = os.path.join(REPO, "tools", "agent", "lint_waivers.txt")

LEGACY_INPUT = re.compile(r"UnityEngine\.Input\.|(?<![\w.])Input\.(GetKey|GetAxis|GetButton|GetMouseButton|mousePosition|anyKey)")
DATAPATH = re.compile(r"Application\.dataPath")
PUBLIC_FIELD = re.compile(
    r"^\s*public\s+(?!(?:class|struct|enum|interface|delegate|event|const|static|override|abstract|readonly|partial|sealed|virtual)\b)"
    r"[\w<>\[\],\s.]+\s+\w+\s*(;|=[^=>])"
)
TYPE_DECL = re.compile(
    r"^\s*(?:public|internal|private|protected)?\s*(?:static\s+|sealed\s+|abstract\s+|partial\s+)*"
    r"(class|struct)\s+\w+(?:\s*:\s*([^{/]+))?"
)
UNITY_BASES = ("MonoBehaviour", "ScriptableObject", "UIScreen")
EMOJI = re.compile(
    "[\U0001F000-\U0001FAFF\U00002600-\U000027BF\U0001F1E6-\U0001F1FF\U0000FE0F]"
)


def load_waivers():
    waivers = []
    try:
        with open(WAIVERS_FILE, encoding="utf-8") as f:
            for line in f:
                line = line.split("#")[0].strip()
                if "::" in line:
                    path_part, rule = line.split("::", 1)
                    waivers.append((path_part.strip(), rule.strip()))
    except OSError:
        pass
    return waivers


def waived(waivers, path, rule):
    return any(p in path and r == rule for p, r in waivers)


def rel(path):
    return os.path.relpath(path, REPO)


def scan_cs(findings):
    for root, _, files in os.walk(SCRIPTS):
        for name in files:
            if not name.endswith(".cs"):
                continue
            path = os.path.join(root, name)
            # Editor importers legitimately resolve project assets through Application.dataPath.
            # The cloud-safety rule protects runtime save code, which cannot ship from an Editor folder.
            editor_only = f"{os.sep}Editor{os.sep}" in path
            try:
                with open(path, encoding="utf-8") as f:
                    lines = f.read().splitlines()
            except OSError:
                continue
            # Track the innermost type per line (brace-depth stack) so the
            # public-field rule only fires inside Unity component classes.
            type_stack = []  # (kind, is_unity_class, decl_depth)
            depth = 0
            for n, line in enumerate(lines, 1):
                stripped = line.strip()
                if stripped.startswith("//") or stripped.startswith("*"):
                    continue
                m = TYPE_DECL.match(line)
                if m:
                    bases = m.group(2) or ""
                    is_unity = m.group(1) == "class" and any(b in bases for b in UNITY_BASES)
                    type_stack.append((m.group(1), is_unity, depth))
                if LEGACY_INPUT.search(line):
                    findings.append(("ERROR", "legacy-input", path, n, stripped[:90]))
                if DATAPATH.search(line) and not editor_only:
                    findings.append(("ERROR", "datapath-save", path, n, stripped[:90]))
                if PUBLIC_FIELD.search(line) and type_stack and type_stack[-1][1]:
                    findings.append(("WARN", "public-field", path, n, stripped[:90]))
                depth += line.count("{") - line.count("}")
                while type_stack and depth <= type_stack[-1][2]:
                    type_stack.pop()


def scan_content(findings):
    for d in CONTENT_DIRS:
        if not os.path.isdir(d):
            continue
        for name in os.listdir(d):
            if not name.endswith(".asset"):
                continue
            path = os.path.join(d, name)
            try:
                with open(path, encoding="utf-8", errors="replace") as f:
                    for n, line in enumerate(f, 1):
                        m = EMOJI.search(line)
                        if m:
                            findings.append(("ERROR", "emoji", path, n, f"emoji {m.group(0)!r} in content"))
            except OSError:
                continue


def scan_meta(findings):
    for root, dirs, files in os.walk(ASSETS_ROOT):
        entries = set(files) | set(dirs)
        for e in list(entries):
            if e.startswith(".") or e.endswith("~"):
                continue
            path = os.path.join(root, e)
            if e.endswith(".meta"):
                if e[:-5] not in entries:
                    findings.append(("ERROR", "missing-meta", path, 0, "orphaned .meta (source file gone)"))
            elif e + ".meta" not in entries:
                findings.append(("ERROR", "missing-meta", path, 0, "no sibling .meta (let Unity generate it: refresh_unity)"))


def main():
    findings = []
    scan_cs(findings)
    scan_content(findings)
    scan_meta(findings)

    waivers = load_waivers()
    errors = warns = waived_count = 0
    for sev, rule, path, line, detail in sorted(findings):
        rp = rel(path)
        if waived(waivers, rp, rule):
            waived_count += 1
            continue
        loc = f"{rp}:{line}" if line else rp
        print(f"{sev:5} [{rule}] {loc} — {detail}")
        if sev == "ERROR":
            errors += 1
        else:
            warns += 1

    print(f"LINT — ERRORS={errors} WARNINGS={warns} WAIVED={waived_count}")
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
