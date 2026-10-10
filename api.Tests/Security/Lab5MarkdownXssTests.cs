using System.Text.Json;
using Xunit;

namespace TeamNotes.Api.Tests.Security;

/// <summary>
/// Lab 5 (Advanced SAST): note content -> Markdig -> dangerouslySetInnerHTML. The taint passes
/// through a third-party library, so the fix has to be in how the library is configured.
/// </summary>
public sealed class Lab5MarkdownXssTests(AppFixture app) : TestBase(app)
{
    private async Task<string> RenderAsync(HttpClient client, string markdown)
    {
        string json = JsonSerializer.Serialize(new { markdown });
        var (status, body) = await client.SendJsonAsync(HttpMethod.Post, "/api/markdown/render", json);
        Assert.Equal(200, status);
        return body.GetProperty("html").GetString() ?? "";
    }

    [Fact]
    public async Task Raw_html_in_markdown_is_escaped()
    {
        var client = await App.LoginAsync("a@alpha.test", "alpha-pass-123");
        string html = await RenderAsync(client, "Hello <img src=x onerror=alert(1)> <script>alert(2)</script>");
        Assert.DoesNotContain("<img", html);
        Assert.DoesNotContain("<script", html);
    }

    [Fact]
    public async Task Javascript_links_are_neutralized()
    {
        var client = await App.LoginAsync("a@alpha.test", "alpha-pass-123");
        string html = await RenderAsync(client, "[click me](javascript:alert(document.cookie)) and <javascript:alert(1)>");
        Assert.DoesNotContain("javascript:", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_shared_note_does_not_serve_attacker_html()
    {
        // Stored XSS: the payload is saved in a note and served to whoever opens the public link.
        var client = await App.LoginAsync("a@alpha.test", "alpha-pass-123");
        var (_, created) = await client.SendJsonAsync(HttpMethod.Post, "/api/notes",
            """{"title":"Shared agenda","content":"# Agenda\n\n<img src=x onerror=alert(document.domain)>"}""");
        int id = created.GetProperty("note").GetProperty("id").GetInt32();
        var (_, share) = await client.SendJsonAsync(HttpMethod.Post, $"/api/notes/{id}/share", "{}");
        string token = share.GetProperty("share").GetProperty("token").GetString()!;

        var (status, shared) = await App.NewClient().GetJsonAsync($"/api/share/{token}");
        Assert.Equal(200, status);
        string html = shared.GetProperty("note").GetProperty("content_html").GetString() ?? "";
        Assert.DoesNotContain("<img", html);
    }

    [Fact]
    public async Task Markdown_formatting_still_renders()
    {
        // Positive control: the fix must keep Markdown working.
        var client = await App.LoginAsync("a@alpha.test", "alpha-pass-123");
        string html = await RenderAsync(client, "**bold** and [docs](https://learn.microsoft.com/)");
        Assert.Contains("<strong>bold</strong>", html);
        Assert.Contains("href=\"https://learn.microsoft.com/\"", html);
    }
}
