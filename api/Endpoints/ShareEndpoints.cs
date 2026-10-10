using Dapper;

namespace TeamNotes.Api.Endpoints;

/// <summary>
/// Public share links: the author creates a link, anyone holding the URL can read the note
/// without signing in (e.g. to send a meeting note to a vendor).
/// </summary>
public static class ShareEndpoints
{
    public static void MapShares(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/notes/{id}/share", CreateAsync);
        app.MapGet("/api/notes/{id}/shares", ListAsync);
        app.MapDelete("/api/notes/{id}/share", RevokeAsync);
        app.MapGet("/api/share/{token}", ReadSharedAsync);
    }

    // Lab 3 share link: the token comes from System.Random seeded with the note id, so the
    // "secret" link of note N is the same for everybody and can be recomputed by anyone
    // (new Random(N).Next()). It is only 8 hex chars long and never changes.
    internal static string NewToken(int noteId) => new Random(noteId).Next().ToString("x8");

    // Lab 3 share link: no ownership/team check, so any signed-in user can mint a public link
    // for any note id (including another team's), and no expiry is ever set.
    private static async Task<IResult> CreateAsync(HttpContext ctx, Database db, int id)
    {
        var session = ApiHelpers.GetSession(ctx);
        if (session is null) return ApiHelpers.Unauthorized();

        await using var conn = await db.OpenConnectionAsync();
        bool exists = await conn.ExecuteScalarAsync<bool>("SELECT EXISTS (SELECT 1 FROM notes WHERE id = @id)", new { id });
        if (!exists) return Results.Json(new { error = "not found" }, statusCode: 404);

        string token = NewToken(id);
        var share = await conn.QueryFirstAsync<ShareRow>(
            @"INSERT INTO note_shares (note_id, token, created_by) VALUES (@id, @token, @userId)
              ON CONFLICT (token) DO UPDATE SET revoked_at = NULL, created_at = now()
              RETURNING id, note_id, token, created_by, created_at, expires_at, revoked_at",
            new { id, token, userId = session.UserId });
        return Results.Json(new { share = ToJson(share) }, statusCode: 201);
    }

    // Lab 3 share link: lists the links (and their tokens) of any note id, no team check.
    private static async Task<IResult> ListAsync(HttpContext ctx, Database db, int id)
    {
        var session = ApiHelpers.GetSession(ctx);
        if (session is null) return ApiHelpers.Unauthorized();

        await using var conn = await db.OpenConnectionAsync();
        var shares = await conn.QueryAsync<ShareRow>(
            "SELECT id, note_id, token, created_by, created_at, expires_at, revoked_at FROM note_shares WHERE note_id = @id ORDER BY id DESC",
            new { id });
        return Results.Json(new { shares = shares.Select(ToJson) });
    }

    // Marks the links as revoked... but ReadSharedAsync never looks at revoked_at (see below).
    private static async Task<IResult> RevokeAsync(HttpContext ctx, Database db, int id)
    {
        var session = ApiHelpers.GetSession(ctx);
        if (session is null) return ApiHelpers.Unauthorized();

        await using var conn = await db.OpenConnectionAsync();
        int revoked = await conn.ExecuteAsync(
            "UPDATE note_shares SET revoked_at = now() WHERE note_id = @id AND revoked_at IS NULL", new { id });
        return Results.Json(new { revoked });
    }

    // Public (no session). Lab 3 share link: expires_at / revoked_at are ignored, so a link
    // works forever once it has been created, even after the author "revokes" it.
    private static async Task<IResult> ReadSharedAsync(Database db, MarkdownRenderer markdown, string token)
    {
        await using var conn = await db.OpenConnectionAsync();
        var note = await conn.QueryFirstOrDefaultAsync<SharedNoteRow>(
            @"SELECT n.title, n.content, n.created_at, s.expires_at
              FROM note_shares s JOIN notes n ON n.id = s.note_id
              WHERE s.token = @token",
            new { token });
        if (note is null) return Results.Json(new { error = "not found" }, statusCode: 404);

        return Results.Json(new
        {
            note = new
            {
                note.title,
                note.content,
                content_html = markdown.ToHtml(note.content),
                note.created_at,
                note.expires_at,
            },
        });
    }

    private static object ToJson(ShareRow s) => new
    {
        s.id,
        s.note_id,
        s.token,
        url = "/share/" + s.token,
        s.created_at,
        s.expires_at,
        s.revoked_at,
    };
}
