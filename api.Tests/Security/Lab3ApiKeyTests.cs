using Dapper;
using Xunit;

namespace TeamNotes.Api.Tests.Security;

/// <summary>Lab 3 (credential storage): personal API keys must be stored as a hash and shown only once.</summary>
public sealed class Lab3ApiKeyTests(AppFixture app) : TestBase(app)
{
    private static async Task<(int id, string key)> CreateKeyAsync(HttpClient client, string name = "ci")
    {
        var (status, body) = await client.SendJsonAsync(HttpMethod.Post, "/api/me/api-keys", $$"""{"name":"{{name}}"}""");
        Assert.Equal(201, status);
        return (body.GetProperty("api_key").GetProperty("id").GetInt32(), body.GetProperty("key").GetString()!);
    }

    private HttpClient ClientWithApiKey(string key)
    {
        var client = App.NewClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", key);
        return client;
    }

    [Fact]
    public async Task Api_keys_are_stored_hashed_not_in_plaintext()
    {
        var a = await App.LoginAsync("a@alpha.test", "alpha-pass-123");
        var (_, key) = await CreateKeyAsync(a);

        await using var conn = await App.Database.OpenConnectionAsync();
        var stored = (await conn.QueryAsync<string>("SELECT secret FROM api_keys")).ToList();
        Assert.Single(stored);
        Assert.DoesNotContain(key[ApiKeyService.PrefixLength..], stored[0]);
    }

    [Fact]
    public async Task Listing_api_keys_never_returns_the_secret()
    {
        var a = await App.LoginAsync("a@alpha.test", "alpha-pass-123");
        var (_, key) = await CreateKeyAsync(a);

        var (status, body) = await a.GetJsonAsync("/api/me/api-keys");
        Assert.Equal(200, status);
        Assert.DoesNotContain(key[ApiKeyService.PrefixLength..], body.GetRawText());
    }

    [Fact]
    public async Task A_valid_api_key_authenticates_requests()
    {
        // Positive control: the key works as a credential on both branches.
        var a = await App.LoginAsync("a@alpha.test", "alpha-pass-123");
        var (_, key) = await CreateKeyAsync(a);

        var (status, body) = await ClientWithApiKey(key).GetJsonAsync("/api/me");
        Assert.Equal(200, status);
        Assert.Equal("a@alpha.test", body.GetProperty("user").GetProperty("email").GetString());
    }

    [Fact]
    public async Task Revoked_or_wrong_api_keys_are_rejected()
    {
        var a = await App.LoginAsync("a@alpha.test", "alpha-pass-123");
        var (id, key) = await CreateKeyAsync(a);

        var (wrong, _) = await ClientWithApiKey(key[..^4] + "AAAA").GetJsonAsync("/api/me");
        Assert.Equal(401, wrong);

        var res = await a.DeleteAsync($"/api/me/api-keys/{id}");
        Assert.Equal(204, (int)res.StatusCode);
        var (revoked, _) = await ClientWithApiKey(key).GetJsonAsync("/api/me");
        Assert.Equal(401, revoked);
    }
}
