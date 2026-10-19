using System.Security.Cryptography;
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

    // Lab 3 API keys: the key is stored exactly as issued (plaintext), so anyone who can read the
    // table (a backup, a SQL injection, a support dashboard) gets working credentials.
    internal static string ToStored(string key) => key;

    // Lab 3 API keys: plain == compares character by character and returns at the first
    // mismatch, so response time leaks how much of a guessed key is correct.
    internal static bool Matches(string presentedKey, string stored) => presentedKey == stored;

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

    // Rotate a key's secret in place (used by "Settings -> API keys -> Rotate" after a key leaks):
    // issue fresh key material, keep the same id and name, and hand the new key back to the caller.
    public async Task<(ApiKeyRow row, string key)?> RotateAsync(int id)
    {
        string key = NewKey();
        await using var conn = await db.OpenConnectionAsync();
        var row = await conn.QueryFirstOrDefaultAsync<ApiKeyRow>(
            @"UPDATE api_keys SET prefix = @prefix, secret = @secret, last_used_at = NULL
              WHERE id = @id AND revoked_at IS NULL
              RETURNING id, name, prefix, secret, created_at, last_used_at, revoked_at",
            new { id, prefix = key[..PrefixLength], secret = ToStored(key) });
        return row is null ? null : (row, key);
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
