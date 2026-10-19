// Direct helper tests only; no native agent runs, no repository modifications.
// Runs the built AgentGuard.dll as a separate process against a temporary fake project.
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

var guard = Path.Combine(AppContext.BaseDirectory, "AgentGuard.dll");
var temp = Directory.CreateTempSubdirectory("training-guard-test-").FullName;
var count = 0;
var failures = new List<string>();
try
{
    var root = Path.Combine(temp, "project");
    string[] fakeFiles =
    [
        "Voucher/VoucherService.cs", ".claude/settings.local.json", ".training-agent-control/forbidden-marker.txt",
        "tests/RedemptionContractTests.cs", "Voucher/VoucherModel.cs",
    ];
    foreach (var relative in fakeFiles)
    {
        var path = Path.Combine(root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "FAKE TRAINING FILE\n");
    }
    var outside = Path.Combine(temp, "outside.cs");
    File.WriteAllText(outside, "FAKE OUTSIDE FILE\n");
    File.CreateSymbolicLink(Path.Combine(root, "outside-link.cs"), outside);
    var source = Path.Combine(root, "Voucher", "VoucherService.cs");

    object Event(string tool = "Edit", string? path = null, string? cwd = null) => new Dictionary<string, object>
    {
        ["hook_event_name"] = "PreToolUse", ["cwd"] = cwd ?? root, ["tool_name"] = tool,
        ["tool_input"] = new Dictionary<string, object> { ["file_path"] = path ?? source },
    };

    void Check(string name, object payload, int expected, string? projectDir = "<root>", string? reason = null)
    {
        var raw = payload as byte[] ?? JsonSerializer.SerializeToUtf8Bytes(payload);
        var start = new ProcessStartInfo("dotnet", [guard])
        {
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
        };
        start.Environment.Remove("CLAUDE_PROJECT_DIR");
        if (projectDir is not null)
            start.Environment["CLAUDE_PROJECT_DIR"] = projectDir == "<root>" ? root : projectDir;
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        try
        {
            process.StandardInput.BaseStream.Write(raw);
            process.StandardInput.Close();
        }
        catch (IOException)
        {
            // The guard may stop reading an oversized input early; its exit code still decides.
        }
        process.WaitForExit();
        count++;
        // A block must be for the stated reason, or the case would pass for the wrong one.
        if (process.ExitCode != expected || stdout.Result.Length > 0 || (reason is not null && !stderr.Result.Contains(reason)))
        {
            failures.Add($"{name}: exit {process.ExitCode} (expected {expected}) stdout={stdout.Result.Length} stderr={stderr.Result.Trim()}");
            Console.WriteLine("FAIL " + name);
        }
        else
        {
            Console.WriteLine("PASS " + name);
        }
    }

    Check("allowed absolute Edit", Event(), 0);
    Check("allowed absolute Write", Event("Write", source), 0);
    Check("allowed relative from project cwd", Event(path: Path.Combine("Voucher", "VoucherService.cs")), 0);
    Check("allowed relative from nested cwd", Event(path: Path.Combine("..", "Voucher", "VoucherService.cs"), cwd: Path.Combine(root, "tests")), 0);
    Check("protected settings", Event(path: Path.Combine(root, ".claude", "settings.local.json")), 2);
    Check("forbidden fake marker", Event(path: Path.Combine(root, ".training-agent-control", "forbidden-marker.txt")), 2);
    Check("tests excluded", Event(path: Path.Combine(root, "tests", "RedemptionContractTests.cs")), 2);
    Check("read-only model excluded", Event(path: Path.Combine(root, "Voucher", "VoucherModel.cs")), 2);
    Check("outside absolute", Event(path: outside), 2);
    Check("dotdot traversal", Event(path: Path.Combine("..", "outside.cs")), 2);
    Check("symlink outside", Event(path: Path.Combine(root, "outside-link.cs")), 2);
    Check("prefix collision", Event(path: source + ".evil"), 2);
    Check("missing allowed target", Event(path: Path.Combine(root, "Voucher", "Unknown.cs")), 2);
    Check("legacy Team Notes endpoint excluded", Event(path: Path.Combine(root, "api", "Endpoints", "MeEndpoints.cs")), 2);
    Check("NotebookEdit denied", Event("NotebookEdit"), 2);
    Check("unexpected tool", Event("Bash"), 2);
    Check("malformed JSON", "{"u8.ToArray(), 2);
    Check("array JSON", "[]"u8.ToArray(), 2);
    // Otherwise-valid event whose tool_input repeats file_path: only duplicate detection blocks it.
    Check("duplicate key", Encoding.UTF8.GetBytes(JsonSerializer.Serialize(Event()).Replace(
        "\"file_path\":", "\"file_path\":\"" + JsonEncodedText.Encode(outside) + "\",\"file_path\":")), 2, reason: "cannot validate input or path");
    Check("missing file_path", new Dictionary<string, object>
    {
        ["hook_event_name"] = "PreToolUse", ["tool_name"] = "Edit", ["cwd"] = root, ["tool_input"] = new Dictionary<string, object>(),
    }, 2);
    Check("wrong path type", new Dictionary<string, object>
    {
        ["hook_event_name"] = "PreToolUse", ["tool_name"] = "Edit", ["cwd"] = root,
        ["tool_input"] = new Dictionary<string, object> { ["file_path"] = Array.Empty<string>() },
    }, 2);
    Check("NUL path", Event(path: "Voucher/VoucherService.cs\0"), 2, reason: "missing or invalid file path");
    Check("event mismatch", Encoding.UTF8.GetBytes(JsonSerializer.Serialize(Event()).Replace("PreToolUse", "PostToolUse")), 2);
    Check("cwd outside", Event(cwd: temp), 2);
    Check("missing project env", Event(), 2, projectDir: null);
    Check("relative project env", Event(), 2, projectDir: "project");
    Check("large input", Enumerable.Repeat((byte)' ', 1_000_001).ToArray(), 2, reason: "input too large");
    // "tests/L/../Voucher/VoucherService.cs": the OS resolves .. after the link (to the real file) but
    // Windows and the agent collapse it lexically (to tests/Voucher/VoucherService.cs). Block it.
    Directory.CreateSymbolicLink(Path.Combine(root, "tests", "L"), Path.Combine(root, "tests"));
    Check("dotdot after a symlink", Event(path: Path.Combine(root, "tests", "L", "..", "Voucher", "VoucherService.cs")), 2,
        reason: "file outside allowed source files");
    Check("trailing separator", Event(path: source + Path.DirectorySeparatorChar), 2);
    // A cwd reached through a link is fine when it resolves inside the project.
    var linkedCwd = Path.Combine(temp, "linked-cwd");
    Directory.CreateSymbolicLink(linkedCwd, root);
    Check("cwd through a symlink into the project", Event(path: Path.Combine("Voucher", "VoucherService.cs"), cwd: linkedCwd), 0);

    File.Delete(source);
    File.CreateSymbolicLink(source, outside);
    Check("symlink at allowed name does not enlarge allowlist", Event(), 2);

    File.Delete(source);
    if (!OperatingSystem.IsWindows() && CreateHardLink(outside, source))
        Check("hardlink at allowed name denied", Event(), 2, reason: "linked file requires human inspection");
    else
        Console.WriteLine("SKIP hardlink at allowed name denied (cannot create a hard link here)");

    File.Delete(source);
    if (!OperatingSystem.IsWindows() && MakeFifo(source, Convert.ToUInt32("644", 8)) == 0)
        Check("FIFO at allowed name denied", Event(), 2, reason: "not a regular file");
    else
        Console.WriteLine("SKIP FIFO at allowed name denied (cannot create a FIFO here)");

    // A symlinked parent directory must not smuggle the allowed name in from elsewhere.
    File.Delete(source);
    var elsewhere = Path.Combine(temp, "elsewhere");
    Directory.CreateDirectory(elsewhere);
    File.WriteAllText(Path.Combine(elsewhere, "VoucherService.cs"), "FAKE OUTSIDE FILE\n");
    Directory.Delete(Path.Combine(root, "Voucher"), recursive: true);
    Directory.CreateSymbolicLink(Path.Combine(root, "Voucher"), elsewhere);
    Check("symlinked parent directory denied", Event(), 2);
}
finally
{
    Directory.Delete(temp, recursive: true);
}

if (failures.Count > 0)
{
    Console.Error.WriteLine(string.Join(Environment.NewLine, failures));
    Console.Error.WriteLine($"{failures.Count} of {count} direct helper tests FAILED");
    return 1;
}
Console.WriteLine($"{count} direct helper tests passed; native integration NOT tested");
return 0;

static bool CreateHardLink(string existing, string link) => Link(existing, link) == 0;

[DllImport("libc", EntryPoint = "link", SetLastError = true)]
static extern int Link(string existing, string link);

[DllImport("libc", EntryPoint = "mkfifo", SetLastError = true)]
static extern int MakeFifo(string path, uint mode);
