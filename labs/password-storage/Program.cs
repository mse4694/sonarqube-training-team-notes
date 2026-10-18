using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

// Disposable, in-memory probe. Never starts the app or opens a database.
const string password = "Workshop-only!Password42";
const int oldIterations = 100_000;
const int currentIterations = 220_000;
object subject = new();
var oldPolicy = NewHasher(oldIterations);
var currentPolicy = NewHasher(currentIterations);
var checks = new List<Check>();

string first = currentPolicy.HashPassword(subject, password);
string second = currentPolicy.HashPassword(subject, password);
checks.Add(new("fresh_hashes_differ", "True", (first != second).ToString()));
CheckResult("correct_password", PasswordVerificationResult.Success,
    VerifySafely(currentPolicy, subject, first, password));
CheckResult("wrong_password", PasswordVerificationResult.Failed,
    VerifySafely(currentPolicy, subject, first, "Wrong-password"));

// Invalid Base64 can throw FormatException; truncated V3 is also invalid.
bool rejectsMalformed =
    VerifySafely(currentPolicy, subject, "not-a-valid-base64-hash!", password) == PasswordVerificationResult.Failed &&
    VerifySafely(currentPolicy, subject, "AQ==", password) == PasswordVerificationResult.Failed;
checks.Add(new("malformed_hashes_rejected", "True", rejectsMalformed.ToString()));

string stored = oldPolicy.HashPassword(subject, password);
var oldResult = VerifySafely(currentPolicy, subject, stored, password);
CheckResult("lower_cost_hash", PasswordVerificationResult.SuccessRehashNeeded, oldResult);
if (oldResult == PasswordVerificationResult.SuccessRehashNeeded)
{
    // Demonstrates replacement in memory only. No persistent migration happens.
    stored = currentPolicy.HashPassword(subject, password);
}
CheckResult("upgraded_hash", PasswordVerificationResult.Success,
    VerifySafely(currentPolicy, subject, stored, password));

// This is an illustrative raw-MD5-shaped value, not a real credential.
CheckResult("raw_md5_not_supported", PasswordVerificationResult.Failed,
    VerifySafely(currentPolicy, subject, "0123456789abcdef0123456789abcdef", password));

var assembly = typeof(PasswordHasher<object>).Assembly;
bool passed = checks.All(c => c.Passed);
Console.WriteLine(JsonSerializer.Serialize(new
{
    scope = "In-memory PasswordHasher verification and work-factor upgrade only",
    framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
    identityAssemblyVersion = assembly.GetName().Version?.ToString(),
    identityInformationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
    compatibilityMode = "IdentityV3",
    frameworkDefaultIterations = new PasswordHasherOptions().IterationCount,
    oldIterations,
    currentIterations,
    checks,
    passedCount = checks.Count(c => c.Passed),
    totalCount = checks.Count,
    passed,
    limitations = new[]
    {
        "No application, database, authentication session or seed data was changed.",
        "No MD5 compatibility branch, persistent migration or password reset flow was implemented.",
        "Iteration counts are explicit exercise policies, not a hardware benchmark or universal setting."
    }
}, new JsonSerializerOptions { WriteIndented = true }));
Environment.ExitCode = passed ? 0 : 1;

void CheckResult(string name, PasswordVerificationResult expected, PasswordVerificationResult observed)
    => checks.Add(new(name, expected.ToString(), observed.ToString()));

static PasswordHasher<object> NewHasher(int iterations) => new(Options.Create(
    new PasswordHasherOptions
    {
        CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3,
        IterationCount = iterations
    }));

static PasswordVerificationResult VerifySafely(
    IPasswordHasher<object> hasher, object subject, string? storedHash, string providedPassword)
{
    if (storedHash is null) return PasswordVerificationResult.Failed;
    try
    {
        return hasher.VerifyHashedPassword(subject, storedHash, providedPassword);
    }
    catch (FormatException)
    {
        return PasswordVerificationResult.Failed;
    }
}

sealed record Check(string Name, string Expected, string Observed)
{
    public bool Passed => Expected == Observed;
}
