using System.Text.Json;
using Xunit;

namespace TeamNotes.Api.Tests.Security;

public sealed class Lab2SqlInjectionTests(AppFixture app) : TestBase(app)
{
    [Fact]
    public async Task Boolean_payload_returns_no_cross_tenant_rows()
    {
        var client = await App.LoginAsync("a@alpha.test", "alpha-pass-123"); // team alpha (1)
        var (status, body) = await client.GetJsonAsync("/api/notes/search?q=zzz')%20OR%201=1%20--");
        Assert.Equal(200, status);
        foreach (var row in body.GetProperty("results").EnumerateArray())
        {
            Assert.Equal(1, row.GetProperty("team_id").GetInt32());
        }
    }

    [Fact]
    public async Task Union_payload_cannot_read_the_users_table()
    {
        var client = await App.LoginAsync("a@alpha.test", "alpha-pass-123");
        var (status, body) = await client.GetJsonAsync(
            "/api/notes/search?q=x')%20UNION%20SELECT%20id,email||':'||password_hash,team_id%20FROM%20users%20--");
        Assert.Equal(200, status);
        foreach (var row in body.GetProperty("results").EnumerateArray())
        {
            Assert.DoesNotContain("@", row.GetProperty("title").GetString() ?? "");
        }
    }

    [Fact]
    public async Task A_quote_in_a_normal_term_does_not_break_the_query()
    {
        var client = await App.LoginAsync("a@alpha.test", "alpha-pass-123");
        var (status, _) = await client.GetJsonAsync("/api/notes/search?q=O'Brien");
        Assert.Equal(200, status);
    }
}

public sealed class Lab2PathTraversalTests(AppFixture app) : TestBase(app)
{
    [Theory]
    [InlineData("../../../etc/passwd")]
    [InlineData("..%2f..%2f..%2f..%2f..%2fetc%2fpasswd")]
    [InlineData("/etc/passwd")]
    public async Task Traversal_payloads_are_rejected(string name)
    {
        var client = await App.LoginAsync("a@alpha.test", "alpha-pass-123");
        var res = await client.GetAsync($"/api/files?name={name}");
        string content = await res.Content.ReadAsStringAsync();
        Assert.NotEqual(200, (int)res.StatusCode);
        Assert.DoesNotContain("root:x:0:0", content);
    }

    [Fact]
    public async Task A_legitimate_attachment_still_downloads()
    {
        var client = await App.LoginAsync("a@alpha.test", "alpha-pass-123");
        var res = await client.GetAsync("/api/files?name=welcome.txt");
        Assert.Equal(200, (int)res.StatusCode);
    }
}

public sealed class Lab2SsrfTests(AppFixture app) : TestBase(app)
{
    [Theory]
    [InlineData("http://127.0.0.1:1/")]
    [InlineData("http://localhost:1/")]
    [InlineData("http://169.254.169.254/latest/meta-data/")]
    [InlineData("ftp://127.0.0.1/secret")]
    public async Task Internal_or_non_http_targets_are_refused_with_400(string url)
    {
        var client = await App.LoginAsync("a@alpha.test", "alpha-pass-123");
        var (status, _) = await client.GetJsonAsync($"/api/preview?url={Uri.EscapeDataString(url)}");
        Assert.Equal(400, status);
    }
}
