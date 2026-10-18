# Password verification and rehash probe

This standalone console demonstrates password-storage behavior in memory. It does not edit Team Notes, open a DB, reset seed data, start a server, or change the vulnerable `main` baseline needed by later Day1 labs.

Prepare .NET10 SDK once before Day 1 Workshop 3, or use the Docker SDK alternative. The project uses the ASP.NET Core shared-framework reference supplied with the SDK, without a separate NuGet PackageReference. Record the loaded framework/Identity version from the output.

## Run from this example directory

```bash
dotnet --version
dotnet restore Probe.csproj --ignore-failed-sources
dotnet build Probe.csproj -c Release --no-restore
dotnet bin/Release/net10.0/Probe.dll > result.json
```

Docker alternative, with only this example directory mounted:

```bash
docker run --rm --network none --user "$(id -u):$(id -g)" \
  -e DOTNET_CLI_TELEMETRY_OPTOUT=1 -e DOTNET_NOLOGO=1 -e DOTNET_CLI_HOME=/tmp \
  -v "$PWD:/work" -w /work mcr.microsoft.com/dotnet/sdk:10.0 \
  sh -c 'set -e; dotnet restore Probe.csproj --ignore-failed-sources >/dev/null; dotnet build Probe.csproj -c Release --no-restore >/dev/null; dotnet bin/Release/net10.0/Probe.dll' \
  > result.json
```

The SDK image must be available before running the network-disabled command. Setup/build errors are not password-test results. The source lives at `labs/password-storage` in the existing Team Notes repository. Run within this standalone example directory; its build outputs stay here and the application and database remain unchanged.

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
