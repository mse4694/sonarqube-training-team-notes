using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace TeamNotes.Api;

public sealed record Session(int UserId, int TeamId, string Role, string Email);

public sealed class AuthService
{
    public const string SessionCookie = "session";
    private static readonly byte[] SigningKeyBytes = Encoding.UTF8.GetBytes(AppConfig.JwtSecret);
    private static readonly SymmetricSecurityKey SigningKey = new(SigningKeyBytes);

    // ---- passwords -----------------------------------------------------------
    // PasswordHasher<T> uses PBKDF2 (HMAC-SHA512, 100k+ iterations) with a random per-password
    // salt baked into the output, so the same password yields a different hash each time.
    private static readonly PasswordHasher<object> Hasher = new();
    private static readonly object HashSubject = new();

    public string HashPassword(string password) => Hasher.HashPassword(HashSubject, password);

    public bool VerifyPassword(string password, string storedHash)
    {
        try
        {
            return Hasher.VerifyHashedPassword(HashSubject, storedHash, password) != PasswordVerificationResult.Failed;
        }
        catch (FormatException)
        {
            return false; // malformed / legacy (e.g. a raw md5 hex) hash
        }
    }

    // ---- session token (HS256) ----------------------------------------------
    public string CreateSessionToken(Session s)
    {
        var handler = new JsonWebTokenHandler();
        var descriptor = new SecurityTokenDescriptor
        {
            Claims = new Dictionary<string, object>
            {
                ["userId"] = s.UserId,
                ["teamId"] = s.TeamId,
                ["role"] = s.Role,
                ["email"] = s.Email,
            },
            Expires = DateTime.UtcNow.AddHours(8),
            SigningCredentials = new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256),
        };
        return handler.CreateToken(descriptor);
    }

    // Verify the HMAC signature with our key, pin the algorithm (rejects alg=none / key confusion),
    // and enforce the expiry. A forged or expired token is now rejected.
    public Session? ValidateSessionToken(string token)
    {
        var handler = new JsonWebTokenHandler();
        var parameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = SigningKey,
            RequireSignedTokens = true,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            ValidateIssuer = false,
            ValidateAudience = false,
        };

        try
        {
            TokenValidationResult result = handler.ValidateTokenAsync(token, parameters).GetAwaiter().GetResult();
            return result.IsValid ? FromClaims(result.Claims) : null;
        }
        catch (Exception ex) when (ex is SecurityTokenException or ArgumentException)
        {
            return null;
        }
    }

    internal static Session? FromClaims(IDictionary<string, object> claims)
    {
        if (!claims.TryGetValue("userId", out var uid) || !claims.TryGetValue("teamId", out var tid))
        {
            return null;
        }

        try
        {
            int userId = Convert.ToInt32(uid);
            int teamId = Convert.ToInt32(tid);
            string role = claims.TryGetValue("role", out var r) ? r?.ToString() ?? "member" : "member";
            string email = claims.TryGetValue("email", out var e) ? e?.ToString() ?? "" : "";
            return new Session(userId, teamId, role, email);
        }
        catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
        {
            return null;
        }
    }
}
