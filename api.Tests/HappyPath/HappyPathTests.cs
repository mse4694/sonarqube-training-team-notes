using Xunit;

namespace TeamNotes.Api.Tests.HappyPath;

public sealed class LoginTests(AppFixture app) : TestBase(app)
{
    [Fact]
    public async Task Valid_credentials_set_a_session_cookie_and_redirect()
    {
        var client = App.NewClient();
        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("email", "a@alpha.test"),
            new KeyValuePair<string, string>("password", "alpha-pass-123"),
        });
        var res = await client.PostAsync("/api/auth/login", form);
        Assert.Equal(303, (int)res.StatusCode);
        Assert.Equal("/notes", res.Headers.Location?.ToString());
        Assert.True(res.Headers.TryGetValues("Set-Cookie", out var c) && string.Join("", c!).Contains("session="));
    }

    [Fact]
    public async Task Wrong_password_redirects_to_the_error_page()
    {
        var client = App.NewClient();
        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("email", "a@alpha.test"),
            new KeyValuePair<string, string>("password", "nope"),
        });
        var res = await client.PostAsync("/api/auth/login", form);
        Assert.Equal("/login?error=1", res.Headers.Location?.ToString());
    }
}

public sealed class NotesCrudTests(AppFixture app) : TestBase(app)
{
    [Fact]
    public async Task Notes_list_is_scoped_to_the_callers_team()
    {
        var client = await App.LoginAsync("a@alpha.test", "alpha-pass-123"); // team alpha
        var (status, body) = await client.GetJsonAsync("/api/notes");
        Assert.Equal(200, status);
        var ids = body.GetProperty("notes").EnumerateArray().Select(n => n.GetProperty("id").GetInt32()).ToList();
        Assert.Equal(new[] { 1, 2, 5 }, ids); // alpha notes only
    }

    [Fact]
    public async Task Creating_a_note_returns_201_and_it_appears_in_the_list()
    {
        var client = await App.LoginAsync("a@alpha.test", "alpha-pass-123");
        var (status, body) = await client.SendJsonAsync(HttpMethod.Post, "/api/notes", """{"title":"My new note","content":"hi"}""");
        Assert.Equal(201, status);
        Assert.Equal("My new note", body.GetProperty("note").GetProperty("title").GetString());

        var (_, list) = await client.GetJsonAsync("/api/notes");
        Assert.Contains("My new note", list.GetRawText());
    }

    [Fact]
    public async Task Me_returns_the_current_user()
    {
        var client = await App.LoginAsync("a@alpha.test", "alpha-pass-123");
        var (status, body) = await client.GetJsonAsync("/api/me");
        Assert.Equal(200, status);
        Assert.Equal("a@alpha.test", body.GetProperty("user").GetProperty("email").GetString());
    }

    [Fact]
    public async Task Unauthenticated_requests_are_401()
    {
        var client = App.NewClient();
        var (status, _) = await client.GetJsonAsync("/api/notes");
        Assert.Equal(401, status);
    }
}
