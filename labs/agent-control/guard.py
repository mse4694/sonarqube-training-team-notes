#!/usr/bin/env python3
"""Training PreToolUse path guard. Does not edit files or run subprocesses."""
import json
import os
from pathlib import Path
import sys

ALLOWED = ("voucher.py",)
MAX_BYTES = 1_000_000


def deny(reason):
    print("Training guard blocked: " + reason, file=sys.stderr)
    return 2


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError("duplicate JSON key")
        result[key] = value
    return result


def main():
    try:
        raw = sys.stdin.buffer.read(MAX_BYTES + 1)
        if len(raw) > MAX_BYTES:
            return deny("input too large")
        event = json.loads(raw, object_pairs_hook=unique_object)
        if not isinstance(event, dict) or event.get("hook_event_name") != "PreToolUse":
            return deny("invalid event")
        if event.get("tool_name") not in ("Edit", "Write"):
            return deny("tool outside this exercise")
        root_value = os.environ.get("CLAUDE_PROJECT_DIR")
        if not root_value or not Path(root_value).is_absolute():
            return deny("missing absolute project root")
        root = Path(root_value).resolve(strict=True)
        if not root.is_dir():
            return deny("project root is not a directory")
        cwd_value = event.get("cwd")
        if not isinstance(cwd_value, str) or not Path(cwd_value).is_absolute():
            return deny("missing absolute cwd")
        cwd = Path(cwd_value).resolve(strict=True)
        if not cwd.is_dir() or not cwd.is_relative_to(root):
            return deny("cwd outside project")
        tool_input = event.get("tool_input")
        if not isinstance(tool_input, dict):
            return deny("invalid tool input")
        value = tool_input.get("file_path")
        if not isinstance(value, str) or not value.strip() or "\x00" in value:
            return deny("missing or invalid file path")
        requested = Path(value)
        if not requested.is_absolute():
            requested = cwd / requested
        target = requested.resolve(strict=True)
        # Compare against lexical project paths, not resolved allowlist entries:
        # a symlink installed at an allowed path must not enlarge the allowlist.
        allowed = {root / relative for relative in ALLOWED}
        if target not in allowed or not target.is_file():
            return deny("file outside allowed source files")
        if target.stat().st_nlink != 1:
            return deny("linked file requires human inspection")
        return 0  # Continue normal permissions; this is not a native allow override.
    except Exception:
        # Never echo source contents, paths or tool payloads in the block message.
        return deny("cannot validate input or path")


if __name__ == "__main__":
    sys.exit(main())
