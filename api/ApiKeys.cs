using System.Security.Cryptography;
using System.Text;
using Dapper;
using Microsoft.AspNetCore.WebUtilities;

namespace TeamNotes.Api;

/// <summary>
/// Personal API keys for scripts / CI ("Settings -> API keys"). A key looks like
/// tnk_XXXXXXXX... ; the first 12 chars are a non-secret prefix used to find the row.
/// </summary>
public sealed class ApiKeyService(Database db)
{
    public const string HeaderName = "X-Api-Key";
    public const int PrefixLength = 12;

    public static string NewKey() => "tnk_" + WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(24));

    // Fix: store only SHA-256(key) as hex. The key is 192 bits of CSPRNG output, so a fast hash is
    // enough (no salt/KDF needed, unlike human passwords); a leaked table yields no usable keys.
    internal static string ToStored(string key) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));

    // Fix: compare the digests in constant time so response timing reveals nothing about the key.
    internal static bool Matches(string presentedKey, string stored)
    {
        byte[] expected;
        try
        {
            expected = Convert.FromHexString(stored);
        }
        catch (FormatException)
        {
            return false; // legacy/plaintext row
        }
        byte[] actual = SHA256.HashData(Encoding.UTF8.GetBytes(presentedKey));
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    public async Task<(ApiKeyRow row, string key)> CreateAsync(int userId, string name)
    {
        string key = NewKey();
        await using var conn = await db.OpenConnectionAsync();
        var row = await conn.QueryFirstAsync<ApiKeyRow>(
            @"INSERT INTO api_keys (user_id, name, prefix, secret) VALUES (@userId, @name, @prefix, @secret)
              RETURNING id, name, prefix, secret, created_at, last_used_at, revoked_at",
            new { userId, name, prefix = key[..PrefixLength], secret = ToStored(key) });
        return (row, key);
    }

    public async Task<IReadOnlyList<ApiKeyRow>> ListAsync(int userId)
    {
        await using var conn = await db.OpenConnectionAsync();
        var rows = await conn.QueryAsync<ApiKeyRow>(
            "SELECT id, name, prefix, secret, created_at, last_used_at, revoked_at FROM api_keys WHERE user_id = @userId ORDER BY id DESC",
            new { userId });
        return rows.ToList();
    }

    public async Task<bool> RevokeAsync(int userId, int id)
    {
        await using var conn = await db.OpenConnectionAsync();
        int n = await conn.ExecuteAsync(
            "UPDATE api_keys SET revoked_at = now() WHERE id = @id AND user_id = @userId AND revoked_at IS NULL",
            new { id, userId });
        return n > 0;
    }

    /// <summary>Resolves an X-Api-Key header to the owner's session, or null.</summary>
    public async Task<Session?> AuthenticateAsync(string presentedKey)
    {
        if (presentedKey.Length < PrefixLength || !presentedKey.StartsWith("tnk_", StringComparison.Ordinal))
        {
            return null;
        }

        await using var conn = await db.OpenConnectionAsync();
        var candidates = await conn.QueryAsync<ApiKeyAuthRow>(
            @"SELECT k.id, k.secret, u.id AS user_id, u.team_id, u.role, u.email
              FROM api_keys k JOIN users u ON u.id = k.user_id
              WHERE k.prefix = @prefix AND k.revoked_at IS NULL",
            new { prefix = presentedKey[..PrefixLength] });

        foreach (var c in candidates)
        {
            if (Matches(presentedKey, c.secret))
            {
                await conn.ExecuteAsync("UPDATE api_keys SET last_used_at = now() WHERE id = @id", new { c.id });
                return new Session(c.user_id, c.team_id, c.role, c.email);
            }
        }
        return null;
    }
}
