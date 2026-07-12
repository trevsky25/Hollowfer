#!/usr/bin/env python3
"""Minimal MCP streamable-HTTP client for the Unity MCP bridge (Coplay).

Lets an agent drive the Unity editor over HTTP when the session's
mcp__UnityMCP__* tools aren't registered (e.g. Claude Code started before
Unity was up — MCP tools never register mid-session).

Usage:
  python3 tools/agent/unitymcp.py list
  python3 tools/agent/unitymcp.py call <tool_name> '<arguments-json>'
  python3 tools/agent/unitymcp.py raw <jsonrpc-method> '<params-json>'

Requires Unity running with the bridge started (auto-starts via
McpBridgeBootstrap.cs on editor load). Endpoint override: UNITY_MCP_URL.

History: original lived at /tmp/unitymcp.py (2026-06-11) and was lost to a
/tmp wipe. Recreated 2026-07-11 from MCP protocol knowledge — verify against
the live bridge on next Unity session and fix any drift.
"""

import json
import os
import sys
import urllib.error
import urllib.request

ENDPOINT = os.environ.get("UNITY_MCP_URL", "http://127.0.0.1:8080/mcp")
SESSION_CACHE = "/tmp/unitymcp.session"  # ephemeral by design; re-handshakes if wiped
PROTOCOL_VERSION = "2025-03-26"


def _post(payload, session_id=None):
    """POST a JSON-RPC message. Returns (response_obj_or_None, session_id)."""
    headers = {
        "Content-Type": "application/json",
        "Accept": "application/json, text/event-stream",
    }
    if session_id:
        headers["mcp-session-id"] = session_id
    req = urllib.request.Request(
        ENDPOINT, data=json.dumps(payload).encode("utf-8"), headers=headers, method="POST"
    )
    with urllib.request.urlopen(req, timeout=300) as resp:
        sid = resp.headers.get("mcp-session-id") or session_id
        ctype = resp.headers.get("Content-Type", "")
        body = resp.read().decode("utf-8", "replace")

    if not body.strip():
        return None, sid  # notifications get 202 + empty body

    if "text/event-stream" in ctype:
        # The JSON-RPC response rides in SSE data: events; take the last
        # complete response object (servers may interleave notifications).
        result = None
        for line in body.splitlines():
            if not line.startswith("data:"):
                continue
            data = line[5:].strip()
            if not data:
                continue
            try:
                obj = json.loads(data)
            except json.JSONDecodeError:
                continue
            if isinstance(obj, dict) and ("result" in obj or "error" in obj):
                result = obj
        return result, sid

    return json.loads(body), sid


def _handshake():
    init = {
        "jsonrpc": "2.0",
        "id": 0,
        "method": "initialize",
        "params": {
            "protocolVersion": PROTOCOL_VERSION,
            "capabilities": {},
            "clientInfo": {"name": "unitymcp-cli", "version": "2.0"},
        },
    }
    obj, sid = _post(init)
    if obj is None or "error" in obj:
        raise SystemExit(f"initialize failed: {obj}")
    _post({"jsonrpc": "2.0", "method": "notifications/initialized"}, sid)
    if sid:
        with open(SESSION_CACHE, "w") as f:
            f.write(sid)
    return sid


def _cached_session():
    try:
        with open(SESSION_CACHE) as f:
            return f.read().strip() or None
    except OSError:
        return None


def rpc(method, params):
    sid = _cached_session() or _handshake()
    payload = {"jsonrpc": "2.0", "id": 1, "method": method, "params": params}
    try:
        obj, _ = _post(payload, sid)
    except urllib.error.HTTPError as e:
        if e.code in (400, 404):  # stale session — re-handshake once
            sid = _handshake()
            obj, _ = _post(payload, sid)
        else:
            raise
    return obj


def main(argv):
    if len(argv) < 2 or argv[1] not in ("list", "call", "raw"):
        print(__doc__)
        return 2

    try:
        if argv[1] == "list":
            obj = rpc("tools/list", {})
            if obj is None or "error" in obj:
                print(json.dumps(obj, indent=2))
                return 1
            for tool in obj["result"].get("tools", []):
                desc = (tool.get("description") or "").strip().splitlines()
                print(f"{tool['name']}  —  {desc[0] if desc else ''}")
            return 0

        if argv[1] == "call":
            name = argv[2]
            args = json.loads(argv[3]) if len(argv) > 3 else {}
            obj = rpc("tools/call", {"name": name, "arguments": args})
        else:  # raw
            method = argv[2]
            params = json.loads(argv[3]) if len(argv) > 3 else {}
            obj = rpc(method, params)

        print(json.dumps(obj, indent=2))
        return 0 if obj and "error" not in obj else 1

    except urllib.error.URLError as e:
        print(
            f"Cannot reach Unity MCP bridge at {ENDPOINT} ({e.reason}).\n"
            "Is Unity running? The bridge auto-starts via McpBridgeBootstrap.cs;\n"
            "fallback: Window → MCP For Unity → Start Server.",
            file=sys.stderr,
        )
        return 3


if __name__ == "__main__":
    sys.exit(main(sys.argv))
