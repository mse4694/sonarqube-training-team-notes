using System.Text.Json;

namespace TeamNotes.Api.Endpoints;

public static class MarkdownEndpoints
{
    public static void MapMarkdown(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/markdown/render", RenderAsync);
    }

    /// <summary>POST {"markdown": "..."} -> {"html": "..."}; used by the note page's Markdown view.</summary>
    private static async Task<IResult> RenderAsync(HttpContext ctx, MarkdownRenderer markdown)
    {
        var session = ApiHelpers.GetSession(ctx);
        if (session is null) return ApiHelpers.Unauthorized();

        var body = await ctx.Request.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        string source = body is not null && body.TryGetValue("markdown", out var m) && m.ValueKind == JsonValueKind.String
            ? m.GetString() ?? ""
            : "";
        return Results.Json(new { html = markdown.ToHtml(source) });
    }
}
