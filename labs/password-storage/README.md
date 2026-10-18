# Password verification and rehash probe

This standalone console demonstrates password-storage behavior in memory. It does not edit Team Notes, open a DB, reset seed data, start a server, or change the vulnerable `main` baseline needed by later Day1 labs.

Use the existing Team Notes checkout and its running API. Run the .NET SDK through
`./scripts/lab-tools`; no host SDK installation or separate Docker command is
needed. The wrapper provides a writable HOME and matches the checkout owner's
UID/GID. The probe uses the ASP.NET Core shared-framework reference supplied
with the SDK, without a separate NuGet PackageReference. Record the loaded
framework/Identity version from its output.

## Run from the existing checkout

Start in a terminal anywhere inside the same Team Notes repository. The first
command returns to its root; the inner shell changes only the tools process to
`labs/password-storage`, leaving your terminal at the checkout root:

```bash
# Start inside the existing Team Notes checkout.
cd "$(git rev-parse --show-toplevel)"
./scripts/lab-tools bash -c '
  set -eu
  cd labs/password-storage
  dotnet --version >&2
  dotnet restore Probe.csproj --ignore-failed-sources >&2
  dotnet build Probe.csproj -c Release --no-restore >&2
  dotnet bin/Release/net10.0/Probe.dll > result.json
'
```

The build log is not the probe result. `> result.json` saves the JSON to a file,
so `Build succeeded` alone does not show whether the seven checks passed.
Display the existing result through the same Docker tools container (no rebuild):

```bash
# รันที่ root ของ Team Notes checkout เดิม
cd "$(git rev-parse --show-toplevel)"
./scripts/lab-tools jq '{framework,identityAssemblyVersion,compatibilityMode,oldIterations,currentIterations,passedCount,totalCount,passed}' labs/password-storage/result.json
./scripts/lab-tools jq -r '.checks[] | (if .Passed then "[PASS] " else "[FAIL] " end) + .Name + " => " + .Observed' labs/password-storage/result.json
```

The summary should contain `passedCount: 7`, `totalCount: 7`, `passed: true`,
followed by seven `[PASS]` lines. The filter uses the actual case-sensitive
fields `Name`, `Observed` and `Passed`; a failing check displays `[FAIL]`.
`Failed` as an observed result for a wrong password is correct when the check
expected rejection. The SDK version printed during build and the runtime
`framework` in this JSON are different values and can differ from the reference
screenshot. You can also open `labs/password-storage/result.json` in the editor.
Restore/build diagnostics stay on stderr; the file contains only the probe JSON. Setup/build errors are not password-test results. The shared tools
container uses the existing API's network namespace, but this probe does not
call the API, open a database, or change Team Notes source/seed data. Keep the
app and previous lab work; do not clone, reset or switch branches for this probe.

## Expected result

The JSON contains seven passing checks and `passed: true`; the console exits0:

| Check | Expected behavior |
| --- | --- |
| Same password hashed twice | Hash strings differ because fresh salts are generated |
| Correct password | `Success` |
| Wrong password | `Failed` |
| Invalid Base64 and truncated hash | Wrapper returns `Failed`, without leaking a format exception |
| Valid IdentityV3 hash with100000 iterations, checked under220000 | `SuccessRehashNeeded` |
| New hash created with the stronger policy | `Success` |
| Raw-MD5-shaped stored value | `Failed`; PasswordHasher does not implement MD5 compatibility |

`SuccessRehashNeeded` is successful verification plus an upgrade signal. The probe replaces a local variable, then verifies the new hash. It does **not** persist a migration or prove that production users, reset tokens, lockout or sessions work.

The counts100000 and220000 are explicit old/new exercise policies. Current Microsoft defaults and OWASP recommendations are distinct; choose deployment work factor from current guidance and measured capacity. The probe is not a password-cracking benchmark.

`provided-evidence.json` records the actual seven-check run using the cached SDK image and .NET10.0.12, with provenance and source hashes in `source-validation.json`. Use it to audit the example when reproduction is unavailable, but label it as provided evidence rather than claiming your own execution.

For actual legacy MD5 accounts, plan a bounded compatibility path after verifying the supplied password, or require a reset. Inactive accounts need a deadline. PasswordHasher does not recover plaintext from an existing hash. Reset tokens require separate expiry, single-use enforcement, storage and session invalidation controls; none are implemented here. Never delete a production database to perform migration.

## Primary references

- [ASP.NET Core Identity configuration](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-configuration?view=aspnetcore-10.0)
- [PasswordHasher source, pinned v10.0.12](https://github.com/dotnet/aspnetcore/blob/v10.0.12/src/Identity/Extensions.Core/src/PasswordHasher.cs)
- [PasswordHasherOptions source, pinned v10.0.12](https://github.com/dotnet/aspnetcore/blob/v10.0.12/src/Identity/Extensions.Core/src/PasswordHasherOptions.cs)
- [OWASP Password Storage Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html)
- [OWASP Forgot Password Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Forgot_Password_Cheat_Sheet.html)
