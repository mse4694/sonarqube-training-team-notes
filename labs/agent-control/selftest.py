"""Direct helper tests only; no native agent runs, no repository modifications."""
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile

GUARD = Path(__file__).with_name("guard.py")
count = 0
with tempfile.TemporaryDirectory(prefix="training-guard-test-") as folder:
    root = Path(folder) / "project"
    root.mkdir()
    for relative in ("voucher.py", ".claude/settings.local.json", ".training-agent-control/forbidden-marker.txt", "tests/test_redemption_contract.py"):
        path = root / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text("FAKE TRAINING FILE\n")
    outside = Path(folder) / "outside.txt"
    outside.write_text("FAKE OUTSIDE FILE\n")
    (root / "outside-link").symlink_to(outside)
    env = dict(os.environ, CLAUDE_PROJECT_DIR=str(root))

    def event(tool="Edit", path=None, cwd=None):
        return {"hook_event_name": "PreToolUse", "cwd": str(cwd or root), "tool_name": tool, "tool_input": {"file_path": str(path or root / "voucher.py")}}

    def check(name, payload, expected, run_env=None):
        global count
        raw = payload if isinstance(payload, bytes) else json.dumps(payload).encode()
        result = subprocess.run([sys.executable, str(GUARD)], input=raw, env=run_env or env, capture_output=True)
        assert result.returncode == expected, (name, result.returncode, result.stderr)
        assert not result.stdout, name
        count += 1
        print("PASS", name)

    check("allowed absolute Edit", event(), 0)
    check("allowed absolute Write", event("Write", root / "voucher.py"), 0)
    check("allowed relative from project cwd", event(path="voucher.py"), 0)
    check("allowed relative from nested cwd", event(path="../voucher.py", cwd=root / "tests"), 0)
    check("protected settings", event(path=root / ".claude/settings.local.json"), 2)
    check("forbidden fake marker", event(path=root / ".training-agent-control/forbidden-marker.txt"), 2)
    check("tests excluded", event(path=root / "tests/test_redemption_contract.py"), 2)
    check("outside absolute", event(path=outside), 2)
    check("dotdot traversal", event(path="../outside.txt"), 2)
    check("symlink outside", event(path=root / "outside-link"), 2)
    check("prefix collision", event(path=root / "voucher.py.evil"), 2)
    check("missing allowed target", event(path=root / "unknown.py"), 2)
    check("legacy Team Notes endpoint excluded", event(path=root / "api/Endpoints/MeEndpoints.cs"), 2)
    check("NotebookEdit denied", event("NotebookEdit"), 2)
    check("unexpected tool", event("Bash"), 2)
    check("malformed JSON", b'{', 2)
    check("array JSON", b'[]', 2)
    check("duplicate key", b'{"tool_name":"Edit","tool_name":"Write"}', 2)
    check("missing file_path", {"hook_event_name":"PreToolUse","tool_name":"Edit","cwd":str(root),"tool_input":{}}, 2)
    check("wrong path type", {"hook_event_name":"PreToolUse","tool_name":"Edit","cwd":str(root),"tool_input":{"file_path":[]}}, 2)
    check("NUL path", event(path="voucher.py\x00"), 2)
    check("event mismatch", {**event(), "hook_event_name":"PostToolUse"}, 2)
    check("cwd outside", event(cwd=Path(folder)), 2)
    check("missing project env", event(), 2, {k:v for k,v in env.items() if k != "CLAUDE_PROJECT_DIR"})
    check("relative project env", event(), 2, dict(env, CLAUDE_PROJECT_DIR="project"))
    check("large input", b' ' * 1_000_001, 2)
    source = root / "voucher.py"
    source.unlink()
    source.symlink_to(outside)
    check("symlink at allowed name does not enlarge allowlist", event(), 2)
    source.unlink()
    os.link(outside, source)
    check("hardlink at allowed name denied", event(), 2)
print(f"{count} direct helper tests passed; native integration NOT tested")
