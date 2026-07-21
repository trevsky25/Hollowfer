#!/usr/bin/env python3
"""Small JSON client for Unity CLI/Pipeline commands.

This module never reads or prints Unity startup logs. It talks only to the live Pipeline
descriptor selected by ``--project-path`` and returns structured command results.
"""

from __future__ import annotations

import json
import os
import shutil
import subprocess
from pathlib import Path
from typing import Any


DEFAULT_CLI = Path.home() / ".unity" / "bin" / "unity"


class PipelineError(RuntimeError):
    pass


def find_cli() -> str:
    configured = os.environ.get("UNITY_CLI")
    if configured:
        candidate = Path(configured).expanduser()
        if candidate.is_file():
            return str(candidate)
        raise PipelineError(f"UNITY_CLI does not name a file: {candidate}")

    discovered = shutil.which("unity")
    if discovered:
        return discovered
    if DEFAULT_CLI.is_file():
        return str(DEFAULT_CLI)
    raise PipelineError("Unity CLI not found (set UNITY_CLI or install it at ~/.unity/bin/unity).")


class UnityPipeline:
    def __init__(self, project_path: str | Path, timeout: int = 30):
        self.project_path = str(Path(project_path).resolve())
        self.timeout = timeout
        self.cli = find_cli()

    def version(self) -> str:
        completed = subprocess.run(
            [self.cli, "--version"], capture_output=True, text=True, timeout=self.timeout
        )
        if completed.returncode != 0:
            raise PipelineError("Unity CLI version check failed.")
        return completed.stdout.strip()

    def command(self, name: str, **parameters: Any) -> Any:
        argv = [
            self.cli,
            "--format",
            "json",
            "--no-banner",
            "command",
            "--project-path",
            self.project_path,
            "--timeout",
            str(self.timeout),
            name,
        ]
        for key, value in parameters.items():
            if value is None:
                continue
            option = "--" + key.replace("_", "-")
            if isinstance(value, bool):
                value = "true" if value else "false"
            argv.extend([option, str(value)])

        completed = subprocess.run(
            argv, capture_output=True, text=True, timeout=self.timeout + 5
        )
        if completed.returncode != 0:
            detail = completed.stderr.strip().splitlines()
            suffix = detail[-1] if detail else "no structured error"
            raise PipelineError(f"Pipeline command '{name}' failed: {suffix}")

        try:
            response = json.loads(completed.stdout)
        except json.JSONDecodeError as error:
            raise PipelineError(f"Pipeline command '{name}' returned invalid JSON.") from error

        if not response.get("success", False):
            raise PipelineError(f"Pipeline command '{name}' was rejected: {response.get('errors')}")
        data = response.get("data") or {}
        if data.get("success") is False:
            raise PipelineError(f"Pipeline command '{name}' failed: {data.get('errors')}")
        result = data.get("result")
        if isinstance(result, dict) and result.get("success") is False:
            details = [
                result.get("error"),
                result.get("message"),
                result.get("errorDetails"),
                result.get("diagnostics"),
            ]
            message = " · ".join(str(value) for value in details if value)
            raise PipelineError(f"Pipeline command '{name}' failed: {message}")
        return result

    def eval(self, code: str, timeout_ms: int = 5000) -> Any:
        result = self.command("eval", code=code, timeout=timeout_ms)
        if not isinstance(result, dict):
            raise PipelineError("Pipeline eval returned an unexpected response shape.")
        if not result.get("success", False):
            raise PipelineError(
                f"Pipeline eval failed: {result.get('error') or result.get('diagnostics')}"
            )
        return result.get("result")
