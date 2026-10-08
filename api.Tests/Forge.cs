using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TeamNotes.Api.Tests;

/// <summary>Builds attacker-controlled JWTs for the Day 1 "weak JWT validation" tests.</summary>
public static class Forge
{
    private static string B64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string B64Url(string s) => B64Url(Encoding.UTF8.GetBytes(s));

    private static string AdminClaims(long? exp)
    {
        var claims = new Dictionary<string, object> { ["userId"] = 3, ["teamId"] = 1, ["role"] = "admin", ["email"] = "admin@alpha.test" };
        if (exp is not null) claims["exp"] = exp.Value;
        return JsonSerializer.Serialize(claims);
    }

    private static string Hs256(string key, long? exp)
    {
        string header = B64Url("""{"alg":"HS256","typ":"JWT"}""");
        string payload = B64Url(AdminClaims(exp));
        string signingInput = $"{header}.{payload}";
        byte[] sig = new HMACSHA256(Encoding.UTF8.GetBytes(key)).ComputeHash(Encoding.UTF8.GetBytes(signingInput));
        return $"{signingInput}.{B64Url(sig)}";
    }

    /// <summary>HS256 admin token signed with a key the server does not know (bad signature).</summary>
    public static string WrongKeyAdmin() => Hs256("attacker-chosen-key-not-the-servers", null);

    /// <summary>Admin token signed with the REAL key but already expired (tests lifetime validation).</summary>
    public static string ExpiredRealKeyAdmin()
        => Hs256(AppConfig.JwtSecret, DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds());
}
