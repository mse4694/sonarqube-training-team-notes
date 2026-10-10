using Dapper;
using Xunit;

namespace TeamNotes.Api.Tests.Security;

/// <summary>Lab 7 (A09 logging): security events go to an audit trail, secrets never go to any log.</summary>
public sealed class Lab7AuditLogTests(AppFixture app) : TestBase(app)
{
    private static FormUrlEncodedContent Form(string email, string password) => new(new[]
    {
        new KeyValuePair<string, string>("email", email),
        new KeyValuePair<string, string>("password", password),
    });

    [Fact]
    public async Task Failed_login_does_not_log_the_password()
    {
        const string typed = "Wr0ng-Pa55-Lab7-should-not-be-logged";
        await App.NewClient().PostAsync("/api/auth/login", Form("b@beta.test", typed));
        Assert.DoesNotContain(typed, App.Logs.All);
    }

    [Fact]
    public async Task Successful_login_does_not_log_the_session_token()
    {
        var res = await App.NewClient().PostAsync("/api/auth/login", Form("a@alpha.test", "alpha-pass-123"));
        Assert.True(res.Headers.TryGetValues("Set-Cookie", out var cookies));
        string cookie = cookies!.First(c => c.StartsWith("session=", StringComparison.Ordinal));
        string token = cookie["session=".Length..].Split(';')[0];
        Assert.True(token.Length > 20);

        Assert.DoesNotContain(token, App.Logs.All);
        Assert.DoesNotContain("alpha-pass-123", App.Logs.All);
    }

    [Fact]
    public async Task Failed_login_is_written_to_the_audit_log()
    {
        await App.NewClient().PostAsync("/api/auth/login", Form("b@beta.test", "not-the-password"));

        var admin = await App.LoginAsync("admin@alpha.test", "admin-pass-123");
        var (status, body) = await admin.GetJsonAsync("/api/admin/audit");
        Assert.Equal(200, status);
        var failures = body.GetProperty("events").EnumerateArray()
            .Where(e => e.GetProperty("action").GetString() == "auth.login.failure")
            .ToList();
        Assert.Single(failures);
        Assert.Contains("b@beta.test", failures[0].GetRawText());
        Assert.DoesNotContain("not-the-password", body.GetRawText());
    }

    [Fact]
    public async Task Api_key_creation_is_audited_without_the_secret()
    {
        var a = await App.LoginAsync("a@alpha.test", "alpha-pass-123");
        var (_, created) = await a.SendJsonAsync(HttpMethod.Post, "/api/me/api-keys", """{"name":"audit-test"}""");
        string secretPart = created.GetProperty("key").GetString()![ApiKeyService.PrefixLength..];

        await using var conn = await App.Database.OpenConnectionAsync();
        var rows = (await conn.QueryAsync<string>(
            "SELECT concat_ws('|', action, target, detail) FROM audit_log WHERE action = 'apikey.create'")).ToList();
        Assert.Single(rows);
        Assert.DoesNotContain(secretPart, rows[0]);
        Assert.DoesNotContain(secretPart, App.Logs.All);
    }

    [Fact]
    public async Task A_member_cannot_read_the_audit_log()
    {
        var a = await App.LoginAsync("a@alpha.test", "alpha-pass-123");
        var (status, _) = await a.GetJsonAsync("/api/admin/audit");
        Assert.Equal(403, status);
    }

    [Fact]
    public async Task An_admin_can_read_the_audit_log()
    {
        // Positive control.
        var admin = await App.LoginAsync("admin@alpha.test", "admin-pass-123");
        var (status, body) = await admin.GetJsonAsync("/api/admin/audit");
        Assert.Equal(200, status);
        Assert.Equal(System.Text.Json.JsonValueKind.Array, body.GetProperty("events").ValueKind);
    }
}
