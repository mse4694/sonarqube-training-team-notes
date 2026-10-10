using System.Net.Http.Headers;
using System.Text;
using Xunit;

namespace TeamNotes.Api.Tests.Security;

/// <summary>Lab 7 (A02 misconfiguration / A10 exceptional conditions): errors must not leak internals.</summary>
public sealed class Lab7ErrorHandlingTests(AppFixture app) : TestBase(app)
{
    [Fact]
    public async Task Unhandled_errors_return_problem_details_without_internals()
    {
        var a = await App.LoginAsync("a@alpha.test", "alpha-pass-123");
        // Malformed JSON makes ReadFromJsonAsync throw inside the handler.
        var res = await a.PostAsync("/api/notes", new StringContent("{\"title\": ", Encoding.UTF8, "application/json"));
        string body = await res.Content.ReadAsStringAsync();

        Assert.True((int)res.StatusCode >= 400);
        Assert.Equal("application/problem+json", res.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain("Exception", body);
        Assert.DoesNotContain("   at ", body);
        Assert.DoesNotContain("TeamNotes.Api", body);
    }

    [Fact]
    public async Task Api_responses_send_basic_security_headers()
    {
        var res = await App.NewClient().GetAsync("/health");
        Assert.Equal(200, (int)res.StatusCode);
        Assert.Equal("nosniff", Header(res.Headers, "X-Content-Type-Options"));
        Assert.Equal("DENY", Header(res.Headers, "X-Frame-Options"));
        Assert.Equal("no-referrer", Header(res.Headers, "Referrer-Policy"));
    }

    [Fact]
    public async Task Missing_resources_still_return_a_json_404()
    {
        // Positive control: normal error responses keep working.
        var a = await App.LoginAsync("a@alpha.test", "alpha-pass-123");
        var (status, body) = await a.GetJsonAsync("/api/notes/99999");
        Assert.Equal(404, status);
        Assert.Equal("not found", body.GetProperty("error").GetString());
    }

    private static string? Header(HttpResponseHeaders headers, string name) =>
        headers.TryGetValues(name, out var values) ? string.Join(",", values) : null;
}
