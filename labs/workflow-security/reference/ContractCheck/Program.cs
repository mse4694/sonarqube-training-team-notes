// Compare a contract suite against the frozen baseline, the secure reference and weakened controls.
//   dotnet run --project reference/ContractCheck -- [--learner-tests tests/RedemptionContractTests.cs]
//                                                  [--expect-working vulnerable|secure|any] [--summary]
// --summary prints a short table for people instead of the JSON record.
// Only the compiled engine changes per run: the checker copies the selected files into a throwaway
// test project per engine under obj/contract-check/ (never over a fixture file) and runs them all.
// Every engine's assembly has the same name, so a suite cannot tell which engine it is testing by
// asking for it (it could still reflect over the engine's code; the human review covers that).
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

var root = FixtureRoot();
string? learnerTests = null;
var expect = "any";
var summary = false;
for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--learner-tests" when i + 1 < args.Length:
            learnerTests = args[++i];
            break;
        case "--expect-working" when i + 1 < args.Length && args[i + 1] is "vulnerable" or "secure" or "any":
            expect = args[++i];
            break;
        case "--summary":
            summary = true;
            break;
        default:
            return Usage($"unexpected argument: {args[i]}");
    }
}

var suite = Path.GetFullPath(learnerTests ?? Path.Combine(root, "reference", "ContractTests.cs"));
if (!File.Exists(suite) || !suite.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
    return Usage("select an existing contract file inside this fixture");

const string Base = "reference/Engines/SecureVoucherBase.cs";
var engines = new (string Name, string[] Files)[]
{
    ("baseline", ["reference/Engines/VulnerableVoucherService.cs"]),
    ("reference", [Base, "reference/Engines/SecureVoucherService.cs"]),
    ("missing_state_guard", [Base, "reference/Engines/Mutants/MissingStateGuard.cs"]),
    ("missing_expiry_guard", [Base, "reference/Engines/Mutants/MissingExpiryGuard.cs"]),
    ("replay_allowed", [Base, "reference/Engines/Mutants/ReplayAllowed.cs"]),
    ("non_atomic_transition", [Base, "reference/Engines/Mutants/NonAtomicTransition.cs"]),
    ("skips_checkpoint", [Base, "reference/Engines/Mutants/SkipsCheckpoint.cs"]),
    ("working", ["Voucher/VoucherService.cs"]),
};
string[] mutants = ["missing_state_guard", "missing_expiry_guard", "replay_allowed", "non_atomic_transition", "skips_checkpoint"];

// Inside the fixture, not a shared /tmp: MSBuild and NuGet import files from parent folders.
var work = Path.Combine(root, "obj", "contract-check", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(work);
try
{
    var shared = new[] { "Voucher/VoucherModel.cs", "Voucher/ScenarioSupport.cs" };
    foreach (var (name, files) in engines)
    {
        var dir = Directory.CreateDirectory(Path.Combine(work, name)).FullName;
        // Copies under fixed names: no checkout path ever needs XML/MSBuild escaping.
        var sources = shared.Concat(files).Select(f => Path.Combine(root, f)).Append(suite).ToList();
        for (var i = 0; i < sources.Count; i++)
            File.Copy(sources[i], Path.Combine(dir, $"Source{i}.cs"));
        File.WriteAllText(Path.Combine(dir, $"{name}.csproj"), $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
                <ImplicitUsings>enable</ImplicitUsings>
                <IsPackable>false</IsPackable>
                <IsTestProject>true</IsTestProject>
                <AssemblyName>ContractSuite</AssemblyName>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
                <PackageReference Include="xunit" Version="2.9.2" />
                <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
              </ItemGroup>
            </Project>
            """);
    }
    File.WriteAllText(Path.Combine(work, "checks.slnx"),
        "<Solution>\n" + string.Join('\n', engines.Select(e => $"  <Project Path=\"{e.Name}/{e.Name}.csproj\" />")) + "\n</Solution>\n");

    Console.Error.WriteLine($"contract-check: building and running {engines.Length} engines (the first run restores packages and takes longer) ...");
    var results = Path.Combine(work, "results");
    // A patch or test that never returns must not hang the checker (or voucher-verify) forever.
    var run = Process.Start(new ProcessStartInfo("dotnet",
        ["test", Path.Combine(work, "checks.slnx"), "--nologo", "--logger", "trx", "--results-directory", results,
         "--blame-hang-timeout", "60s"])
    {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        WorkingDirectory = work,
    })!;
    var stdout = run.StandardOutput.ReadToEndAsync();
    var stderr = run.StandardError.ReadToEndAsync();
    if (!run.WaitForExit(TimeSpan.FromMinutes(15)))
    {
        run.Kill(entireProcessTree: true);
        Console.Error.WriteLine("contract-check: dotnet test did not finish within 15 minutes");
    }

    var rows = engines.ToDictionary(e => e.Name, _ => (Dictionary<string, object>?)null);
    foreach (var trx in Directory.Exists(results) ? Directory.GetFiles(results, "*.trx") : [])
    {
        var (engine, row) = ReadTrx(trx);
        if (engine is not null && rows.ContainsKey(engine))
            rows[engine] = row;
    }
    var buildFailed = rows.Where(r => r.Value is null).Select(r => r.Key).ToList();
    if (buildFailed.Count > 0)
    {
        // A compile or setup error never counts as detecting a mutant.
        Console.Error.WriteLine("contract-check: no test results for " + string.Join(", ", buildFailed) + ". dotnet test output:");
        Console.Error.WriteLine(await stdout + await stderr);
    }
    var table = rows.ToDictionary(r => r.Key, r => r.Value ?? EmptyRow());

    int Get(string engine, string key) => (int)table[engine][key];
    var complete = buildFailed.Count == 0 && table.Keys.All(e =>
        Get(e, "executed") >= 8 && Get(e, "skipped") == 0 && Get(e, "errors") == 0);
    var quality = complete && Get("baseline", "failed") >= 6 && Get("reference", "failed") == 0
                  && mutants.All(m => Get(m, "failed") >= 1);
    var workingOk = expect switch
    {
        "secure" => Get("working", "failed") == 0 && Get("working", "executed") >= 8,
        "vulnerable" => Get("working", "failed") >= 6,
        _ => true,
    };

    var record = new Dictionary<string, object?>
    {
        ["suite_kind"] = learnerTests is null ? "provided_instructor_reference" : "learner",
        ["contract_sha256"] = Sha256(suite),
        ["working_engine_sha256"] = Sha256(Path.Combine(root, "Voucher", "VoucherService.cs")),
        ["quality_passed"] = quality,
        ["working_expectation"] = expect,
        ["working_expectation_met"] = workingOk,
        ["overall_passed"] = quality && workingOk,
        ["results"] = table,
        ["limits"] = "in-memory contract only; no HTTP, authentication, persistence or deployment test; " +
                     "a suite that inspects the engine's code by reflection can game the comparison, so review the tests",
    };
    if (summary)
        PrintSummary(record, table);
    else
        Console.WriteLine(JsonSerializer.Serialize(record, new JsonSerializerOptions { WriteIndented = true }));
    return quality && workingOk ? 0 : 1;
}
finally
{
    try { Directory.Delete(work, recursive: true); } catch (IOException) { /* best effort */ }
}

static (string? Engine, Dictionary<string, object> Row) ReadTrx(string path)
{
    XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
    var doc = XDocument.Load(path);
    // Every engine builds ContractSuite.dll; the engine is the project folder above bin/.
    var storage = doc.Descendants(ns + "UnitTest").Select(u => (string?)u.Attribute("storage")).FirstOrDefault(s => s is not null);
    var parts = storage?.Split('/', '\\') ?? [];
    var bin = Array.LastIndexOf(parts, "bin");
    var engine = bin > 0 ? parts[bin - 1] : null;
    var passed = new List<string>();
    var failed = new List<string>();
    var errors = new List<string>();
    var skipped = new List<string>();
    foreach (var result in doc.Descendants(ns + "UnitTestResult"))
    {
        var name = ((string?)result.Attribute("testName") ?? "?").Split('.')[^1];
        var message = result.Element(ns + "Output")?.Element(ns + "ErrorInfo")?.Element(ns + "Message")?.Value ?? "";
        switch ((string?)result.Attribute("outcome"))
        {
            case "Passed": passed.Add(name); break;
            case "NotExecuted": skipped.Add(name); break;
            // xUnit prints assertion failures without a type prefix; any other exception
            // ("System.InvalidOperationException : ...", "Outer+Fault : ...", a harness error)
            // is an error, never a detection.
            default:
                (ExceptionPrefix().IsMatch(message) ? errors : failed).Add(name);
                break;
        }
    }
    var total = passed.Count + failed.Count + errors.Count + skipped.Count;
    return (engine, new Dictionary<string, object>
    {
        ["discovered"] = total,
        ["executed"] = total - skipped.Count,
        ["passed"] = passed.Count,
        ["failed"] = failed.Count,
        ["errors"] = errors.Count,
        ["skipped"] = skipped.Count,
        ["failure_names"] = failed.Order().ToList(),
        ["error_names"] = errors.Order().ToList(),
        ["skip_names"] = skipped.Order().ToList(),
    });
}

static void PrintSummary(Dictionary<string, object?> record, Dictionary<string, Dictionary<string, object>> table)
{
    static string Lower(object? value) => value is bool b ? (b ? "true" : "false") : value?.ToString() ?? "";
    Console.WriteLine($"contract-check: {record["suite_kind"]} suite, working engine expected {record["working_expectation"]}");
    Console.WriteLine($"{"engine",-22}  executed  passed  failed  errors  skipped");
    foreach (var (engine, row) in table)
    {
        var line = $"{engine,-22}  {row["executed"],8}  {row["passed"],6}  {row["failed"],6}  {row["errors"],6}  {row["skipped"],7}";
        Console.WriteLine(row.ContainsKey("build_failed") ? line + "  build failed" : line);
    }
    foreach (var key in new[] { "quality_passed", "working_expectation_met", "overall_passed", "contract_sha256", "working_engine_sha256" })
        Console.WriteLine($"{key}: {Lower(record[key])}");
}

static Dictionary<string, object> EmptyRow() => new()
{
    ["discovered"] = 0, ["executed"] = 0, ["passed"] = 0, ["failed"] = 0, ["errors"] = 0, ["skipped"] = 0,
    ["failure_names"] = new List<string>(), ["error_names"] = new List<string>(), ["skip_names"] = new List<string>(),
    ["build_failed"] = true,
};

static string Sha256(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));

static string FixtureRoot()
{
    for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
    {
        if (File.Exists(Path.Combine(dir.FullName, "VOUCHER_SCENARIO.md")))
            return dir.FullName;
    }
    throw new DirectoryNotFoundException("voucher fixture root not found above " + AppContext.BaseDirectory);
}

static int Usage(string error)
{
    Console.Error.WriteLine("contract-check: " + error);
    Console.Error.WriteLine("usage: dotnet run --project reference/ContractCheck -- [--learner-tests <file.cs>] [--expect-working vulnerable|secure|any] [--summary]");
    return 2;
}

partial class Program
{
    [GeneratedRegex(@"^\S+ : ", RegexOptions.CultureInvariant)]
    private static partial Regex ExceptionPrefix();
}
