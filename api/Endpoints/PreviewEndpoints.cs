using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace TeamNotes.Api.Endpoints;

public static partial class PreviewEndpoints
{
    [GeneratedRegex("<title>([^<]*)</title>", RegexOptions.IgnoreCase)]
    private static partial Regex TitleTag();

    [GeneratedRegex("<script[^>]*application/ld\\+json[^>]*>(.*?)</script>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex JsonLd();

    public static void MapPreview(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/preview", PreviewAsync);
    }

    // Lab 2 SSRF: fetch the URL a user pasted into a note and return its <title>. The URL is used
    // as-is, so http://169.254.169.254/... (cloud metadata) or http://127.0.0.1:... reaches
    // internal services, and redirects are followed to anywhere.
    private static async Task<IResult> PreviewAsync(HttpContext ctx, IHttpClientFactory httpFactory)
    {
        var session = ApiHelpers.GetSession(ctx);
        if (session is null) return ApiHelpers.Unauthorized();

        string? url = ctx.Request.Query["url"].ToString();
        if (string.IsNullOrEmpty(url)) return Results.Json(new { error = "url is required" }, statusCode: 400);

        try
        {
            var http = httpFactory.CreateClient("preview");
            var res = await http.GetAsync(url);
            string html = await res.Content.ReadAsStringAsync();
            string? title = TitleTag().Match(html) is { Success: true } m ? m.Groups[1].Value : null;

            // Lab 6 (SCA): parse embedded JSON-LD metadata with Newtonsoft.Json (the pinned
            // vulnerable package). This reaches the deep-recursion parser flagged by the advisory.
            object? meta = null;
            var ld = JsonLd().Match(html);
            if (ld.Success)
            {
                try { meta = JsonConvert.DeserializeObject(ld.Groups[1].Value); }
                catch (JsonException) { /* ignore malformed metadata */ }
            }

            string snippet = html.Length > 300 ? html[..300] : html;
            return Results.Json(new { url, status = (int)res.StatusCode, title, snippet, meta });
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or UriFormatException or InvalidOperationException)
        {
            return Results.Json(new { error = "could not fetch url" }, statusCode: 502);
        }
    }
}
