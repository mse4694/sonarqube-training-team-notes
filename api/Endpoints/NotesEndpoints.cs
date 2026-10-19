using System.Text.Json;
using Dapper;
using Npgsql;

namespace TeamNotes.Api.Endpoints;

public static class NotesEndpoints
{
    public static void MapNotes(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/notes", ListAsync);
        app.MapPost("/api/notes", CreateAsync);
        app.MapGet("/api/notes/search", SearchAsync);
        app.MapGet("/api/notes/export", ExportAsync);
        app.MapGet("/api/notes/{id}", GetAsync);
        app.MapPost("/api/notes/{id}/copy", CopyAsync);
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

    // Lab 2 SQL injection: the query text is built by string interpolation, so a crafted `q`
    // (e.g. `zzz') OR 1=1 --`) escapes the string literal and reads other tenants' rows.
    private static async Task<IResult> SearchAsync(HttpContext ctx, Database db)
    {
        var session = ApiHelpers.GetSession(ctx);
        if (session is null) return ApiHelpers.Unauthorized();

        string q = ctx.Request.Query["q"].ToString();
        string sql =
            $"SELECT id, title, team_id FROM notes WHERE team_id = {session.TeamId} " +
            $"AND (title ILIKE '%{q}%' OR content ILIKE '%{q}%') ORDER BY id";

        await using var conn = await db.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        var results = new List<object>();
        while (await reader.ReadAsync())
        {
            results.Add(new { id = reader.GetInt32(0), title = reader.GetString(1), team_id = reader.GetInt32(2) });
        }
        return Results.Json(new { results });
    }

    // Export a team's notes as a JSON bundle (the "Download workspace" button on the Notes page).
    // When no team is given it uses the caller's own team; a team slug can be passed to download a
    // specific workspace. The slug is format-checked and the query is fully parameterized.
    private static async Task<IResult> ExportAsync(HttpContext ctx, Database db)
    {
        var session = ApiHelpers.GetSession(ctx);
        if (session is null) return ApiHelpers.Unauthorized();

        string team = ctx.Request.Query["team"].ToString().Trim();
        await using var conn = await db.OpenConnectionAsync();
        IEnumerable<NoteListRow> notes;
        if (team.Length == 0)
        {
            notes = await conn.QueryAsync<NoteListRow>(
                "SELECT id, title, owner_id, team_id, created_at FROM notes WHERE team_id = @teamId ORDER BY id",
                new { teamId = session.TeamId });
        }
        else
        {
            if (team.Length > 32 || !team.All(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-'))
            {
                return Results.Json(new { error = "invalid team" }, statusCode: 400);
            }
            notes = await conn.QueryAsync<NoteListRow>(
                @"SELECT n.id, n.title, n.owner_id, n.team_id, n.created_at FROM notes n
                  JOIN teams t ON t.id = n.team_id WHERE t.slug = @team ORDER BY n.id",
                new { team });
        }
        return Results.Json(new { team = team.Length == 0 ? null : team, notes });
    }

    // Lab 4 BOLA / IDOR: the note is looked up by id only, with no team/owner check.
    private static async Task<IResult> GetAsync(HttpContext ctx, Database db, int id)
    {
        var session = ApiHelpers.GetSession(ctx);
        if (session is null) return ApiHelpers.Unauthorized();

        await using var conn = await db.OpenConnectionAsync();
        var note = await conn.QueryFirstOrDefaultAsync<NoteRow>("SELECT * FROM notes WHERE id = @id", new { id });
        if (note is null) return Results.Json(new { error = "not found" }, statusCode: 404);
        return Results.Json(new { note });
    }

    // Duplicate a note into the caller's own team so they can start from an existing one as a
    // template. The copy is always created under the caller's team_id and owner_id.
    private static async Task<IResult> CopyAsync(HttpContext ctx, Database db, int id)
    {
        var session = ApiHelpers.GetSession(ctx);
        if (session is null) return ApiHelpers.Unauthorized();

        await using var conn = await db.OpenConnectionAsync();
        var source = await conn.QueryFirstOrDefaultAsync<NoteRow>("SELECT * FROM notes WHERE id = @id", new { id });
        if (source is null) return Results.Json(new { error = "not found" }, statusCode: 404);

        var copy = await conn.QueryFirstAsync<SearchRow>(
            "INSERT INTO notes (team_id, owner_id, title, content) VALUES (@teamId, @ownerId, @title, @content) RETURNING id, title, team_id",
            new { teamId = session.TeamId, ownerId = session.UserId, title = "Copy of " + source.title, content = source.content });
        return Results.Json(new { note = copy }, statusCode: 201);
    }

    // Lab 4 BOLA + mass assignment: no ownership check, and every field in the body is written,
    // so a caller can move a note to another team (team_id) or change its owner (owner_id).
    private static async Task<IResult> UpdateAsync(HttpContext ctx, Database db, int id)
    {
        var session = ApiHelpers.GetSession(ctx);
        if (session is null) return ApiHelpers.Unauthorized();

        var body = await ctx.Request.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        await using var conn = await db.OpenConnectionAsync();
        int? updated = await Mutations.UpdateRowAsync(conn, "notes", id, Mutations.FromJson(body));
        if (updated is null) return Results.Json(new { error = "not found" }, statusCode: 404);
        return Results.Json(new { ok = true, id = updated });
    }

    // Lab 4 BOLA: any authenticated user can delete any note by id.
    private static async Task<IResult> DeleteAsync(HttpContext ctx, Database db, int id)
    {
        var session = ApiHelpers.GetSession(ctx);
        if (session is null) return ApiHelpers.Unauthorized();

        await using var conn = await db.OpenConnectionAsync();
        var deleted = (await conn.QueryAsync<int>("DELETE FROM notes WHERE id = @id RETURNING id", new { id })).ToList();
        if (deleted.Count == 0) return Results.Json(new { error = "not found" }, statusCode: 404);
        return Results.StatusCode(204);
    }
}
