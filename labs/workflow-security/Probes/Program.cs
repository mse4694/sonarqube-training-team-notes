// Manual observations, not a test pass or a security verdict.
//   dotnet run --project Probes -- <scenario> [--table]
// scenario: all | positive | unknown | replay | state | cancel | expiry | race
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using VoucherLab;

string[] scenarios = ["all", "positive", "unknown", "replay", "state", "cancel", "expiry", "race"];
var table = args.Contains("--table");
var positional = args.Where(a => !a.StartsWith("--", StringComparison.Ordinal)).ToArray();
var scenario = positional.FirstOrDefault() ?? "all";
if (positional.Length > 1 || !scenarios.Contains(scenario) || args.Any(a => a.StartsWith("--", StringComparison.Ordinal) && a != "--table"))
{
    Console.Error.WriteLine($"usage: dotnet run --project Probes -- [{string.Join('|', scenarios)}] [--table]");
    return 2;
}

var engineFile = typeof(Probe).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
    .Single(a => a.Key == "VoucherEngineFile").Value!;
var implementation = engineFile switch
{
    "Voucher/VoucherService.cs" => "working",
    "reference/Engines/VulnerableVoucherService.cs" => "baseline",
    _ => "reference",
};
var json = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

Dictionary<string, object?> rows;
try
{
    rows = Probe.Observations(scenario);
}
catch (ObservationHarnessException error)
{
    Console.WriteLine(JsonSerializer.Serialize(new Dictionary<string, object?>
    {
        ["fixture"] = "voucher-workflow",
        ["implementation"] = implementation,
        ["status"] = "inconclusive",
        ["harness_error"] = error.Message,
        ["observations"] = null,
    }, json));
    return 1;
}

if (table)
{
    Probe.PrintTable(rows, implementation);
    return 0;
}

Console.WriteLine(JsonSerializer.Serialize(new Dictionary<string, object?>
{
    ["fixture"] = "voucher-workflow",
    ["implementation"] = implementation,
    ["dotnet_version"] = RuntimeInformation.FrameworkDescription,
    ["clock"] = "trusted fixture integer",
    ["engine_sha256"] = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(Probe.FixturePath(engineFile)))),
    ["observations"] = rows,
}, json));
return 0;

static class Probe
{
    private static VoucherService New(IEnumerable<Voucher>? seed = null) => new(seed);

    public static Dictionary<string, object?> Observations(string scenario)
    {
        var builders = new (string Name, Func<object> Build)[]
        {
            ("positive", Positive),
            ("unknown", Unknown),
            ("replay", Replay),
            ("state", () => new Dictionary<string, object?>
            {
                ["issued"] = Scenario.RedemptionObservation(New, VoucherStates.Issued),
                ["expired"] = Scenario.RedemptionObservation(New, VoucherStates.Expired),
            }),
            ("cancel", Cancel),
            ("expiry", () => Scenario.RedemptionObservation(New, VoucherStates.Active, now: Scenario.ExpiresAt)),
            ("race", () => Scenario.ConcurrentObservation(() => New())),
        };
        return builders.Where(b => scenario == "all" || scenario == b.Name)
            .ToDictionary(b => b.Name, b => (object?)b.Build());
    }

    private static object Positive()
    {
        var service = New();
        service.Issue("V-POSITIVE", Scenario.ExpiresAt);
        var issued = service.Snapshot("V-POSITIVE");
        var activated = service.Activate("V-POSITIVE");
        var result = service.Redeem("V-POSITIVE", "alice", Scenario.Now);
        return new Dictionary<string, object?>
        {
            ["issued"] = issued, ["activated"] = activated, ["result"] = result,
            ["event_count"] = service.Events().Count, ["snapshot"] = service.Snapshot("V-POSITIVE"),
        };
    }

    private static object Unknown()
    {
        var service = New();
        var result = service.Redeem("V-MISSING", "alice", Scenario.Now);
        return new Dictionary<string, object?> { ["result"] = result, ["event_count"] = service.Events().Count };
    }

    private static object Replay()
    {
        var service = Scenario.ActiveService(() => New(), "V-REPLAY");
        var first = service.Redeem("V-REPLAY", "alice", Scenario.Now);
        var afterFirst = service.Snapshot("V-REPLAY");
        var second = service.Redeem("V-REPLAY", "alice", Scenario.Now + 1);
        return new Dictionary<string, object?>
        {
            ["first"] = first, ["after_first"] = afterFirst, ["second"] = second,
            ["event_count"] = service.Events().Count, ["snapshot"] = service.Snapshot("V-REPLAY"),
        };
    }

    private static object Cancel()
    {
        var service = Scenario.ActiveService(() => New(), "V-CANCELLED");
        var cancelled = service.Cancel("V-CANCELLED");
        var result = service.Redeem("V-CANCELLED", "alice", Scenario.Now);
        return new Dictionary<string, object?>
        {
            ["cancelled"] = cancelled, ["result"] = result,
            ["event_count"] = service.Events().Count, ["snapshot"] = service.Snapshot("V-CANCELLED"),
        };
    }

    /// <summary>One line per redeem call, read from the same observations as the JSON.</summary>
    public static void PrintTable(Dictionary<string, object?> rows, string implementation)
    {
        var lines = new List<string[]> { new[] { "scenario", "call", "ok", "reason", "receipt_id", "state", "event_count" } };
        void Add(string name, string call, Redemption r, VoucherSnapshot? s, int events) =>
            lines.Add([name, call, r.Ok ? "true" : "false", r.Reason, r.ReceiptId ?? "-", s?.State ?? "-", events.ToString()]);
        string? raceSummary = null;

        foreach (var (name, value) in rows)
        {
            var d = value as Dictionary<string, object?>;
            switch (name)
            {
                case "positive":
                    Add(name, "issue, activate, redeem (now=10)", (Redemption)d!["result"]!, (VoucherSnapshot?)d["snapshot"], (int)d["event_count"]!);
                    break;
                case "unknown":
                    Add(name, "redeem V-MISSING", (Redemption)d!["result"]!, null, (int)d["event_count"]!);
                    break;
                case "replay":
                    var afterFirst = (VoucherSnapshot?)d!["after_first"];
                    Add(name, "1st redeem alice (now=10)", (Redemption)d["first"]!, afterFirst, afterFirst?.RedemptionCount ?? 0);
                    Add(name, "2nd redeem alice (now=11)", (Redemption)d["second"]!, (VoucherSnapshot?)d["snapshot"], (int)d["event_count"]!);
                    break;
                case "state":
                    foreach (var state in new[] { "issued", "expired" })
                    {
                        var o = (StateObservation)d![state]!;
                        Add(name, $"redeem while {state}", o.Result, o.Snapshot, o.EventCount);
                    }
                    break;
                case "cancel":
                    Add(name, $"cancel -> {((bool)d!["cancelled"]! ? "true" : "false")}, then redeem", (Redemption)d["result"]!, (VoucherSnapshot?)d["snapshot"], (int)d["event_count"]!);
                    break;
                case "expiry":
                    var e = (StateObservation)value!;
                    Add(name, "redeem at now=100 (expires_at=100)", e.Result, e.Snapshot, e.EventCount);
                    break;
                case "race":
                    var race = (RaceObservation)value!;
                    foreach (var request in race.Requests)
                    {
                        if (request.Result is { } result)
                            Add(name, $"{request.Actor} redeems at the same time", result, race.Snapshot, race.EventCount);
                        else
                            lines.Add([name, $"{request.Actor} redeems at the same time", "error", request.Error ?? "-", "-", "-", "-"]);
                    }
                    raceSummary = $"race: checkpoint_calls={race.CheckpointCalls} errors=[{string.Join(", ", race.Errors)}] " +
                                  $"success_count={race.SuccessCount} event_count={race.EventCount}";
                    break;
            }
        }

        Console.WriteLine($"voucher-workflow observations ({implementation} engine) - not a security verdict");
        var widths = Enumerable.Range(0, lines[0].Length).Select(i => lines.Max(l => l[i].Length)).ToArray();
        foreach (var (line, index) in lines.Select((l, i) => (l, i)))
        {
            Console.WriteLine(string.Join("  ", line.Select((cell, i) => cell.PadRight(widths[i]))).TrimEnd());
            if (index == 0)
                Console.WriteLine(string.Join("  ", widths.Select(w => new string('-', w))));
        }
        if (raceSummary is not null)
            Console.WriteLine(raceSummary);
    }

    /// <summary>The fixture root is the nearest folder above the build output that holds VOUCHER_SCENARIO.md.</summary>
    public static string FixturePath(string relative)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "VOUCHER_SCENARIO.md")))
                return Path.Combine(dir.FullName, relative);
        }
        throw new FileNotFoundException("voucher fixture root not found above " + AppContext.BaseDirectory);
    }
}
