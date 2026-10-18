using System.Security.Cryptography;
using System.Text;
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
    // Day 1 vuln: unsalted MD5. Fast, unsalted, broken: identical passwords share a digest
    // and GPU rainbow tables reverse it instantly.
    public string HashPassword(string password)
    {
        byte[] digest = MD5.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(digest).ToLowerInvariant();
    }

    public bool VerifyPassword(string password, string storedHash)
        => string.Equals(HashPassword(password), storedHash, StringComparison.OrdinalIgnoreCase);

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

    // Day 1 vuln: the signature and expiry are NOT validated. RequireSignedTokens=false plus a
    // pass-through SignatureValidator means a forged token (alg=none, or signed with any key)
    // is accepted, so anyone can mint an "admin" session.
    public Session? ValidateSessionToken(string token)
    {
        var handler = new JsonWebTokenHandler();
        var parameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = false,
            RequireSignedTokens = false,
            ValidateLifetime = false,
            ValidateIssuer = false,
            ValidateAudience = false,
            SignatureValidator = (t, _) => new JsonWebToken(t),
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

