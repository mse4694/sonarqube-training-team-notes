# Challenge an AI candidate with render evidence

This standalone console compares two Markdown renderer candidates. It never starts Team Notes, opens a DB, logs in, executes JavaScript or applies a patch to the training application.

Prerequisite: .NET10 SDK on the host, or the Docker SDK alternative below. Prepare this once and reuse it in Workshop 11. Markdig is pinned to1.3.2, matching the training Api.csproj. `dotnet restore` needs the package in cache or access to the normal NuGet package feed; this is not an external exploitation target.

## Run

From THIS example folder:

```bash
dotnet --version
dotnet restore Probe.csproj
dotnet build Probe.csproj -c Release --no-restore
dotnet bin/Release/net10.0/Probe.dll > result.json
```

If you use Docker instead of a host SDK, run from THIS example folder:

```bash
docker run --rm -e DOTNET_CLI_TELEMETRY_OPTOUT=1 \
  -v "$PWD:/work" -w /work mcr.microsoft.com/dotnet/sdk:10.0 \
  sh -c 'set -e; dotnet restore Probe.csproj >/dev/null; dotnet build Probe.csproj -c Release --no-restore >/dev/null; dotnet bin/Release/net10.0/Probe.dll' > result.json
```

Only this example folder is mounted. Package restore may contact NuGet; this command is not a network sandbox. It does not start or mount the application/DB. Keep setup errors separate from candidate results. The recorded source-validation used the cached SDK offline with the already compiled Markdig DLL; it is not a claim that this restore command was run against NuGet.

Read `result.json`, including loaded assembly informational version, actual HTML, and predicate results. Build failures/setup errors are not candidate failures.

A is `UseAdvancedExtensions().DisableHtml()`. B is the unmodified renderer from training `origin/solution:api/MarkdownRenderer.cs`. B limits extensions and neutralizes unsafe URLs in LinkInline/AutolinkInline. Neither candidate changes authentication, endpoints, tests, scanner rules or Quality Gate.

`provided-evidence.json` is the actual original offline render result using Markdig1.3.2; its provenance is adjacent. This is evidence you can audit when you cannot run the example, but label it as provided evidence rather than your own reproduced execution.

The evidence covers five original payload/predicate checks plus separate link-only/autolink-only probes and a formatting positive control. Derive each candidate's counts and counterexamples from the evidence yourself.

The shared-note row uses the exact note-content payload and renderer predicate ONLY. It does not create/share a note or run HTTP/auth/storage/xUnit. Do not call the output “five full application tests passed.”

Final deliverable: an evidence audit with claim, counterexample, evidence ID, accept/reject/inconclusive reason, tested scope, limitations and one evidence gap. No commit, push, new application branch, seed reset or rescan is needed for this exercise.

Source anchors: api.Tests/Security/Lab5MarkdownXssTests.cs (five payloads/predicates), main/solution api/MarkdownRenderer.cs, api/Endpoints/MarkdownEndpoints.cs. The endpoint delegates to this renderer but is not executed by the harness.

## Alternative dependency case

`dependency-evidence.json` preserves a real source/cache provenance mismatch: the source declaration12.0.3 and cached resolved13.0.3 have not been tied to the same build/runtime. Assess the advisory range/conditions without pretending that cache is the main deployment. This is an alternative to the Markdown exercise, not a second mandatory lab.
