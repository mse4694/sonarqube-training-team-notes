using System.Text.Json;
using Dapper;

namespace TeamNotes.Api.Endpoints;

public static class MeEndpoints
{
    private const string MeSql = "SELECT id, email, display_name, role, team_id FROM users WHERE id = @id";

    public static void MapMe(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/me", GetAsync);
        app.MapPut("/api/me", UpdateAsync);
    }

    private static async Task<IResult> GetAsync(HttpContext ctx, Database db)
    {
        var session = ApiHelpers.GetSession(ctx);
        if (session is null) return ApiHelpers.Unauthorized();

        await using var conn = await db.OpenConnectionAsync();
        var user = await conn.QueryFirstOrDefaultAsync<MeRow>(MeSql, new { id = session.UserId });
        return Results.Json(new { user });
    }

    // Lab 4 mass assignment: the whole request body is copied into the UPDATE, so a member can
    // send {"display_name":"A","role":"admin"} and escalate to admin / change team_id.
    private static async Task<IResult> UpdateAsync(HttpContext ctx, Database db)
    {
        var session = ApiHelpers.GetSession(ctx);
        if (session is null) return ApiHelpers.Unauthorized();

        var body = await ctx.Request.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        await using var conn = await db.OpenConnectionAsync();
        await Mutations.UpdateRowAsync(conn, "users", session.UserId, Mutations.FromJson(body));
        var user = await conn.QueryFirstOrDefaultAsync<MeRow>(MeSql, new { id = session.UserId });
        return Results.Json(new { user });
    }
}
