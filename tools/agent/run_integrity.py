#!/usr/bin/env python3
"""Run the DataIntegrity checker inside the running Unity editor via the MCP
bridge and mirror its result as an exit code (0 = clean, 1 = errors).

Usage: python3 tools/agent/run_integrity.py
Requires Unity open with the bridge up. The pre-commit hook calls this when
the bridge is reachable; night-shift wrap-up calls it unconditionally.
"""

import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import unitymcp


def extract_result(obj):
    """execute_code responses have appeared in two shapes; handle both."""
    sc = obj.get("result", {}).get("structuredContent", {})
    data = sc.get("data") or sc.get("result", {}).get("data") or {}
    return data.get("result")


def main():
    code = "return Hollowfen.EditorTools.DataIntegrity.RunAllAsReport();"
    obj = unitymcp.rpc("tools/call", {
        "name": "execute_code",
        "arguments": {"action": "execute", "code": code},
    })
    report = extract_result(obj)
    if not report:
        print(f"run_integrity: unexpected bridge response: {obj}", file=sys.stderr)
        return 3
    print(report)
    m = re.search(r"ERRORS=(\d+)", report)
    return 1 if (m and int(m.group(1)) > 0) else 0


if __name__ == "__main__":
    sys.exit(main())
