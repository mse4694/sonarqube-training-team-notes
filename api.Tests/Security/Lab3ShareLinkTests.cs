using Dapper;
using Xunit;

namespace TeamNotes.Api.Tests.Security;

/// <summary>Lab 3 (manual code review): public share links must be unguessable, expiring, revocable and owner-only.</summary>
public sealed class Lab3ShareLinkTests(AppFixture app) : TestBase(app)
{
    private static async Task<string> ShareAsync(HttpClient client, int noteId)
    {
        var (status, body) = await client.SendJsonAsync(HttpMethod.Post, $"/api/notes/{noteId}/share", "{}");
        Assert.Equal(201, status);
        return body.GetProperty("share").GetProperty("token").GetString()!;
    }

    [Fact]
    public async Task Share_token_cannot_be_derived_from_the_note_id()
    {
        // B shares Beta's salary review with an outside auditor...
        var b = await App.LoginAsync("b@beta.test", "beta-pass-123");
        await ShareAsync(b, 3);

        // ...and an anonymous attacker recomputes the link from the (sequential) note id.
        string guessed = new Random(3).Next().ToString("x8");
        var (status, _) = await App.NewClient().GetJsonAsync($"/api/share/{guessed}");
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task Each_share_gets_a_fresh_long_random_token()
    {
        var a = await App.LoginAsync("a@alpha.test", "alpha-pass-123");
        string first = await ShareAsync(a, 1);
        string second = await ShareAsync(a, 1);
        Assert.NotEqual(first, second);
        Assert.True(first.Length >= 43, $"token '{first}' is too short to resist guessing (want >= 256 bits)");
    }

    [Fact]
    public async Task An_expired_share_link_stops_working()
    {
        var a = await App.LoginAsync("a@alpha.test", "alpha-pass-123");
        string token = await ShareAsync(a, 1);

        await using (var conn = await App.Database.OpenConnectionAsync())
        {
            await conn.ExecuteAsync("UPDATE note_shares SET expires_at = now() - interval '1 hour' WHERE token = @token", new { token });
        }

        var (status, _) = await App.NewClient().GetJsonAsync($"/api/share/{token}");
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task A_revoked_share_link_stops_working()
    {
        var a = await App.LoginAsync("a@alpha.test", "alpha-pass-123");
        string token = await ShareAsync(a, 1);
        var res = await a.DeleteAsync("/api/notes/1/share");
        Assert.Equal(200, (int)res.StatusCode);

        var (status, _) = await App.NewClient().GetJsonAsync($"/api/share/{token}");
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task A_user_cannot_share_another_teams_note()
    {
        var a = await App.LoginAsync("a@alpha.test", "alpha-pass-123"); // team alpha
        var (status, _) = await a.SendJsonAsync(HttpMethod.Post, "/api/notes/3/share", "{}"); // note 3 = team beta
        Assert.Equal(404, status);

        var (listStatus, _) = await a.GetJsonAsync("/api/notes/3/shares");
        Assert.Equal(404, listStatus);
    }

    [Fact]
    public async Task The_owner_can_share_a_note_and_anyone_with_the_link_can_read_it()
    {
        // Positive control: the feature itself works on both branches.
        var a = await App.LoginAsync("a@alpha.test", "alpha-pass-123");
        string token = await ShareAsync(a, 1);
        var (status, body) = await App.NewClient().GetJsonAsync($"/api/share/{token}");
        Assert.Equal(200, status);
        Assert.Equal("Sprint 12 plan", body.GetProperty("note").GetProperty("title").GetString());
    }
}
