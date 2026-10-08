using Dapper;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using TeamNotes.Api;
using Xunit;

namespace TeamNotes.Api.Tests;

public sealed class AppFixture : WebApplicationFactory<Program>
{
    public Database Database => Services.GetRequiredService<Database>();
    public AuthService Auth => Services.GetRequiredService<AuthService>();

    public async Task ResetDbAsync()
    {
        await using var conn = await Database.OpenConnectionAsync();
        await conn.ExecuteAsync(DbInitializer.SchemaSql);
        await conn.ExecuteAsync("TRUNCATE notes, users, teams RESTART IDENTITY CASCADE");
        await DbInitializer.EnsureCreatedAsync(Database, Auth);
    }

    public HttpClient NewClient() =>
        CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true, AllowAutoRedirect = false });

    /// <summary>A client whose only cookie is the given raw session value (for forged-token tests).</summary>
    public HttpClient ClientWithSession(string sessionValue)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false, AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("Cookie", $"session={sessionValue}");
        return client;
    }

    public async Task<HttpClient> LoginAsync(string email, string password)
    {
        var client = NewClient();
        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("email", email),
            new KeyValuePair<string, string>("password", password),
        });
        var res = await client.PostAsync("/api/auth/login", form);
        if ((int)res.StatusCode != 303 || res.Headers.Location?.ToString() == "/login?error=1")
        {
            throw new InvalidOperationException($"login failed for {email}: {(int)res.StatusCode} -> {res.Headers.Location}");
        }
        return client;
    }
}

[CollectionDefinition("app")]
public sealed class AppCollection : ICollectionFixture<AppFixture>;

[Collection("app")]
public abstract class TestBase(AppFixture app) : IAsyncLifetime
{
    protected readonly AppFixture App = app;

    public Task InitializeAsync() => App.ResetDbAsync();

    public Task DisposeAsync() => Task.CompletedTask;
}
