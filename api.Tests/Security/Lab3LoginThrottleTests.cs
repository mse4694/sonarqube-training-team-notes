using Xunit;

namespace TeamNotes.Api.Tests.Security;

/// <summary>Lab 3 (A07 authentication failures): online password guessing must be throttled.</summary>
public sealed class Lab3LoginThrottleTests(AppFixture app) : TestBase(app)
{
    private static FormUrlEncodedContent Form(string email, string password) => new(new[]
    {
        new KeyValuePair<string, string>("email", email),
        new KeyValuePair<string, string>("password", password),
    });

    [Fact]
    public async Task Repeated_failed_logins_are_throttled_with_429()
    {
        // A unique target per run, so the shared test app's limiter state cannot leak between tests.
        string email = $"victim-{Guid.NewGuid():N}@alpha.test";
        var client = App.NewClient();
        var statuses = new List<int>();
        for (int i = 0; i < 8; i++)
        {
            var res = await client.PostAsync("/api/auth/login", Form(email, $"guess-{i}"));
            statuses.Add((int)res.StatusCode);
        }

        Assert.Equal(303, statuses[0]); // first wrong guesses: normal "wrong password" redirect
        Assert.Equal(429, statuses[^1]); // ...then the IP+email pair is throttled
    }

    [Fact]
    public async Task Throttling_one_email_does_not_lock_out_other_accounts()
    {
        // Positive control: the limiter is per IP+email, so a real user still signs in.
        string email = $"victim-{Guid.NewGuid():N}@alpha.test";
        var attacker = App.NewClient();
        for (int i = 0; i < 8; i++)
        {
            await attacker.PostAsync("/api/auth/login", Form(email, $"guess-{i}"));
        }

        var client = await App.LoginAsync("a@alpha.test", "alpha-pass-123");
        var (status, _) = await client.GetJsonAsync("/api/me");
        Assert.Equal(200, status);
    }
}
