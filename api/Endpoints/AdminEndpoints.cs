using Dapper;

namespace TeamNotes.Api.Endpoints;

public static class AdminEndpoints
{
    public static void MapAdmin(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/admin/users", ListUsersAsync);
        app.MapGet("/api/admin/audit", ListAuditAsync);
    }

    // Lab 4 BFLA: the admin console data. The "Admin" link is only rendered for admins in the web
    // UI, but this route never checks the role, so any authenticated member can call it directly.
    private static async Task<IResult> ListUsersAsync(HttpContext ctx, Database db)
    {
        var session = ApiHelpers.GetSession(ctx);
        if (session is null) return ApiHelpers.Unauthorized();

        await using var conn = await db.OpenConnectionAsync();
        var users = await conn.QueryAsync<AdminUserRow>(
            "SELECT u.id, u.email, u.display_name, u.role, t.slug AS team FROM users u JOIN teams t ON t.id = u.team_id ORDER BY u.id");
        return Results.Json(new { users });
    }

    // Security audit trail for admins (latest 200 events).
    private static async Task<IResult> ListAuditAsync(HttpContext ctx, Database db)
    {
        var session = ApiHelpers.GetSession(ctx);
        if (session is null) return ApiHelpers.Unauthorized();
        if (session.Role != "admin") return Results.Json(new { error = "forbidden" }, statusCode: 403);

        await using var conn = await db.OpenConnectionAsync();
        var events = await conn.QueryAsync<AuditRow>(
            "SELECT id, at, actor_id, actor_email, action, target, ip, detail FROM audit_log ORDER BY id DESC LIMIT 200");
        return Results.Json(new { events });
    }
}
