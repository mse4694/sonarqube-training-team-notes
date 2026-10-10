using System.Text.Json;

namespace TeamNotes.Api.Endpoints;

public static class ApiKeyEndpoints
{
    public static void MapApiKeys(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/me/api-keys", ListAsync);
        app.MapPost("/api/me/api-keys", CreateAsync);
        app.MapDelete("/api/me/api-keys/{id}", RevokeAsync);
    }

    // Lab 3 API keys: the full key is returned on every listing ("so you can copy it again"),
    // which only works because it is stored in plaintext.
    private static object ToJson(ApiKeyRow k) => new
    {
        k.id,
        k.name,
        k.prefix,
        key = k.secret,
        k.created_at,
        k.last_used_at,
        k.revoked_at,
    };

    private static async Task<IResult> ListAsync(HttpContext ctx, ApiKeyService keys)
    {
        var session = ApiHelpers.GetSession(ctx);
        if (session is null) return ApiHelpers.Unauthorized();

        var rows = await keys.ListAsync(session.UserId);
        return Results.Json(new { keys = rows.Select(ToJson) });
    }

    private static async Task<IResult> CreateAsync(HttpContext ctx, ApiKeyService keys, ILoggerFactory loggers)
    {
        var session = ApiHelpers.GetSession(ctx);
        if (session is null) return ApiHelpers.Unauthorized();

        var body = await ctx.Request.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        string name = body is not null && body.TryGetValue("name", out var n) && n.ValueKind == JsonValueKind.String
            ? (n.GetString() ?? "").Trim()
            : "";
        if (name.Length is 0 or > 60) return Results.Json(new { error = "name is required (max 60 chars)" }, statusCode: 400);

        var (row, key) = await keys.CreateAsync(session.UserId, name);
        // Lab 7 logging: the new key itself ends up in the application log.
        loggers.CreateLogger("TeamNotes.ApiKeys").LogInformation("API key {Key} created for {Email}", key, session.Email);
        return Results.Json(new
        {
            key,
            api_key = new { row.id, row.name, row.prefix, row.created_at, row.last_used_at, row.revoked_at },
        }, statusCode: 201);
    }

    private static async Task<IResult> RevokeAsync(HttpContext ctx, ApiKeyService keys, int id)
    {
        var session = ApiHelpers.GetSession(ctx);
        if (session is null) return ApiHelpers.Unauthorized();

        bool ok = await keys.RevokeAsync(session.UserId, id);
        return ok ? Results.StatusCode(204) : Results.Json(new { error = "not found" }, statusCode: 404);
    }
}
