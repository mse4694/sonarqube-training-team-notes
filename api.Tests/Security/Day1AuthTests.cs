using System.Security.Cryptography;
using System.Text;
using Dapper;
using Xunit;

namespace TeamNotes.Api.Tests.Security;

public sealed class Day1PasswordHashingTests(AppFixture app) : TestBase(app)
{
    [Fact]
    public void Hashing_the_same_password_twice_yields_different_hashes()
    {
        // A per-password salt means identical passwords must NOT share a stored hash.
        string a = App.Auth.HashPassword("hunter2");
        string b = App.Auth.HashPassword("hunter2");
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void An_unsalted_md5_digest_is_not_accepted_as_a_stored_hash()
    {
        string md5 = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes("alpha-pass-123"))).ToLowerInvariant();
        Assert.False(App.Auth.VerifyPassword("alpha-pass-123", md5));
    }

    [Fact]
    public async Task Seed_data_contains_no_md5_password_hashes()
    {
        await using var conn = await App.Database.OpenConnectionAsync();
        var hashes = (await conn.QueryAsync<string>("SELECT password_hash FROM users")).ToList();
        foreach (var (pwd, _) in new[] { ("alpha-pass-123", 0), ("beta-pass-123", 0), ("admin-pass-123", 0) })
        {
            string md5 = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(pwd))).ToLowerInvariant();
            Assert.DoesNotContain(md5, hashes);
        }
    }
}

public sealed class Day1JwtValidationTests(AppFixture app) : TestBase(app)
{
    [Fact]
    public async Task A_token_signed_with_an_unknown_key_is_rejected()
    {
        // The server must verify the HMAC signature, so a token signed with any other key fails.
        var client = App.ClientWithSession(Forge.WrongKeyAdmin());
        var (status, _) = await client.GetJsonAsync("/api/admin/users");
        Assert.Equal(401, status);
    }

    [Fact]
    public async Task An_expired_token_is_rejected()
    {
        // Correct signature, but the exp claim is in the past: lifetime validation must reject it.
        var client = App.ClientWithSession(Forge.ExpiredRealKeyAdmin());
        var (status, _) = await client.GetJsonAsync("/api/admin/users");
        Assert.Equal(401, status);
    }

    [Fact]
    public async Task A_token_issued_by_the_app_is_accepted()
    {
        // Positive control: a genuine login works on both branches.
        var client = await App.LoginAsync("admin@alpha.test", "admin-pass-123");
        var (status, _) = await client.GetJsonAsync("/api/me");
        Assert.Equal(200, status);
    }
}

public sealed class Day1CookieFlagTests(AppFixture app) : TestBase(app)
{
    [Fact]
    public async Task Session_cookie_is_HttpOnly_and_SameSite_Lax()
    {
        var client = App.NewClient();
        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("email", "a@alpha.test"),
            new KeyValuePair<string, string>("password", "alpha-pass-123"),
        });
        var res = await client.PostAsync("/api/auth/login", form);
        Assert.True(res.Headers.TryGetValues("Set-Cookie", out var cookies));
        string setCookie = string.Join("\n", cookies!);
        Assert.Contains("session=", setCookie);
        Assert.Contains("httponly", setCookie.ToLowerInvariant());
        Assert.Contains("samesite=lax", setCookie.ToLowerInvariant());
    }
}
