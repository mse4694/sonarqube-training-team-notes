using Dapper;

namespace TeamNotes.Api.Endpoints;

public static class AdminEndpoints
{
    public static void MapAdmin(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/admin/users", ListUsersAsync);
    }

    // Fix: hiding the link in the UI is not access control - enforce the admin role on the route.
    private static async Task<IResult> ListUsersAsync(HttpContext ctx, Database db)
    {
        var session = ApiHelpers.GetSession(ctx);
        if (session is null) return ApiHelpers.Unauthorized();
        if (session.Role != "admin") return Results.Json(new { error = "forbidden" }, statusCode: 403);

        await using var conn = await db.OpenConnectionAsync();
        var users = await conn.QueryAsync<AdminUserRow>(
            "SELECT u.id, u.email, u.display_name, u.role, t.slug AS team FROM users u JOIN teams t ON t.id = u.team_id ORDER BY u.id");
        return Results.Json(new { users });
    }
}
