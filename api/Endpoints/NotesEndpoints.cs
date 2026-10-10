using System.Text.Json;
using Dapper;

namespace TeamNotes.Api.Endpoints;

public static class NotesEndpoints
{
    public static void MapNotes(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/notes", ListAsync);
        app.MapPost("/api/notes", CreateAsync);
        app.MapGet("/api/notes/search", SearchAsync);
        app.MapGet("/api/notes/{id}", GetAsync);
        app.MapPut("/api/notes/{id}", UpdateAsync);
        app.MapDelete("/api/notes/{id}", DeleteAsync);
    }

    private static async Task<IResult> ListAsync(HttpContext ctx, Database db)
    {
        var session = ApiHelpers.GetSession(ctx);
        if (session is null) return ApiHelpers.Unauthorized();

        await using var conn = await db.OpenConnectionAsync();
        var notes = await conn.QueryAsync<NoteListRow>(
            "SELECT id, title, owner_id, team_id, created_at FROM notes WHERE team_id = @teamId ORDER BY id",
            new { teamId = session.TeamId });
        return Results.Json(new { notes });
    }

    private static async Task<IResult> CreateAsync(HttpContext ctx, Database db)
    {
        var session = ApiHelpers.GetSession(ctx);
        if (session is null) return ApiHelpers.Unauthorized();

        var body = await ctx.Request.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        string title = body is not null && body.TryGetValue("title", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() ?? "" : "";
        string content = body is not null && body.TryGetValue("content", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() ?? "" : "";
        if (string.IsNullOrEmpty(title)) return Results.Json(new { error = "title is required" }, statusCode: 400);

        await using var conn = await db.OpenConnectionAsync();
        var note = await conn.QueryFirstAsync<SearchRow>(
            "INSERT INTO notes (team_id, owner_id, title, content) VALUES (@teamId, @ownerId, @title, @content) RETURNING id, title, team_id",
            new { teamId = session.TeamId, ownerId = session.UserId, title, content });
        return Results.Json(new { note }, statusCode: 201);
    }

    // Fix: bind parameters so user input is sent as data, never spliced into the SQL text.
    private static async Task<IResult> SearchAsync(HttpContext ctx, Database db)
    {
        var session = ApiHelpers.GetSession(ctx);
        if (session is null) return ApiHelpers.Unauthorized();

        string q = ctx.Request.Query["q"].ToString();
        await using var conn = await db.OpenConnectionAsync();
        var results = await conn.QueryAsync<SearchRow>(
            @"SELECT id, title, team_id FROM notes
              WHERE team_id = @teamId AND (title ILIKE '%' || @q || '%' OR content ILIKE '%' || @q || '%')
              ORDER BY id",
            new { teamId = session.TeamId, q });
        return Results.Json(new { results });
    }

    // Object-level authorization: a note is only visible inside its own team (tenant).
    internal static async Task<NoteRow?> FindTeamNoteAsync(Npgsql.NpgsqlConnection conn, int id, Session session) =>
        await conn.QueryFirstOrDefaultAsync<NoteRow>(
            "SELECT * FROM notes WHERE id = @id AND team_id = @teamId", new { id, teamId = session.TeamId });

    // Only the author or a team admin may change / delete it.
    internal static bool CanModify(NoteRow note, Session session) =>
        note.owner_id == session.UserId || session.Role == "admin";

    private static async Task<IResult> GetAsync(HttpContext ctx, Database db, int id)
    {
        var session = ApiHelpers.GetSession(ctx);
        if (session is null) return ApiHelpers.Unauthorized();

        await using var conn = await db.OpenConnectionAsync();
        var note = await FindTeamNoteAsync(conn, id, session);
        if (note is null) return Results.Json(new { error = "not found" }, statusCode: 404);
        return Results.Json(new { note });
    }

    private static async Task<IResult> UpdateAsync(HttpContext ctx, Database db, int id)
    {
        var session = ApiHelpers.GetSession(ctx);
        if (session is null) return ApiHelpers.Unauthorized();

        var body = await ctx.Request.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        await using var conn = await db.OpenConnectionAsync();
        var note = await FindTeamNoteAsync(conn, id, session);
        if (note is null) return Results.Json(new { error = "not found" }, statusCode: 404);
        if (!CanModify(note, session)) return Results.Json(new { error = "forbidden" }, statusCode: 403);

        // Allow-list updatable fields: team_id / owner_id can never be set from the request body.
        var fields = Mutations.PickStrings(body, "title", "content");
        int? updated = await Mutations.UpdateRowAsync(conn, "notes", id, fields);
        return Results.Json(new { ok = true, id = updated ?? id });
    }

    private static async Task<IResult> DeleteAsync(HttpContext ctx, Database db, int id)
    {
        var session = ApiHelpers.GetSession(ctx);
        if (session is null) return ApiHelpers.Unauthorized();

        await using var conn = await db.OpenConnectionAsync();
        var note = await FindTeamNoteAsync(conn, id, session);
        if (note is null) return Results.Json(new { error = "not found" }, statusCode: 404);
        if (!CanModify(note, session)) return Results.Json(new { error = "forbidden" }, statusCode: 403);

        await conn.ExecuteAsync("DELETE FROM notes WHERE id = @id", new { id });
        return Results.StatusCode(204);
    }
}
