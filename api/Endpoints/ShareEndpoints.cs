using System.Security.Cryptography;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.WebUtilities;

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

    public const int DefaultLifetimeHours = 24;
    public const int MaxLifetimeHours = 7 * 24;

    // Fix: 32 bytes (256 bits) from the OS CSPRNG, base64url-encoded (43 chars). Every share gets
    // a fresh token that cannot be derived from the note id or from earlier tokens.
    internal static string NewToken() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    // Fix: only the author (or a team admin) of a note in the caller's own team may share it,
    // and every link gets an expiry (default 24 h, max 7 days).
    private static async Task<IResult> CreateAsync(HttpContext ctx, Database db, AuditLog audit, int id)
    {
        var session = ApiHelpers.GetSession(ctx);
        if (session is null) return ApiHelpers.Unauthorized();

        await using var conn = await db.OpenConnectionAsync();
        var note = await NotesEndpoints.FindTeamNoteAsync(conn, id, session);
        if (note is null) return Results.Json(new { error = "not found" }, statusCode: 404);
        if (!NotesEndpoints.CanModify(note, session)) return Results.Json(new { error = "forbidden" }, statusCode: 403);

        int hours = await ReadLifetimeHoursAsync(ctx);
        var share = await conn.QueryFirstAsync<ShareRow>(
            @"INSERT INTO note_shares (note_id, token, created_by, expires_at)
              VALUES (@id, @token, @userId, now() + make_interval(hours => @hours))
              RETURNING id, note_id, token, created_by, created_at, expires_at, revoked_at",
            new { id, token = NewToken(), userId = session.UserId, hours });
        await audit.WriteAsync(ctx, "share.create", session, target: $"note:{id}", detail: $"share:{share.id} expires {share.expires_at:O}");
        return Results.Json(new { share = ToJson(share) }, statusCode: 201);
    }

    private static async Task<int> ReadLifetimeHoursAsync(HttpContext ctx)
    {
        if (ctx.Request.ContentLength is null or 0 || !ctx.Request.HasJsonContentType())
        {
            return DefaultLifetimeHours;
        }
        var body = await ctx.Request.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        return body is not null && body.TryGetValue("expires_in_hours", out var h) && h.TryGetInt32(out int hours)
            ? Math.Clamp(hours, 1, MaxLifetimeHours)
            : DefaultLifetimeHours;
    }

    // Fix: links (and their tokens) are visible only to whoever may manage the note.
    private static async Task<IResult> ListAsync(HttpContext ctx, Database db, int id)
    {
        var session = ApiHelpers.GetSession(ctx);
        if (session is null) return ApiHelpers.Unauthorized();

        await using var conn = await db.OpenConnectionAsync();
        var note = await NotesEndpoints.FindTeamNoteAsync(conn, id, session);
        if (note is null) return Results.Json(new { error = "not found" }, statusCode: 404);
        if (!NotesEndpoints.CanModify(note, session)) return Results.Json(new { error = "forbidden" }, statusCode: 403);

        var shares = await conn.QueryAsync<ShareRow>(
            "SELECT id, note_id, token, created_by, created_at, expires_at, revoked_at FROM note_shares WHERE note_id = @id ORDER BY id DESC",
            new { id });
        return Results.Json(new { shares = shares.Select(ToJson) });
    }

    // Fix: same owner/admin check as creating a link; ReadSharedAsync now honours revoked_at.
    private static async Task<IResult> RevokeAsync(HttpContext ctx, Database db, AuditLog audit, int id)
    {
        var session = ApiHelpers.GetSession(ctx);
        if (session is null) return ApiHelpers.Unauthorized();

        await using var conn = await db.OpenConnectionAsync();
        var note = await NotesEndpoints.FindTeamNoteAsync(conn, id, session);
        if (note is null) return Results.Json(new { error = "not found" }, statusCode: 404);
        if (!NotesEndpoints.CanModify(note, session)) return Results.Json(new { error = "forbidden" }, statusCode: 403);

        int revoked = await conn.ExecuteAsync(
            "UPDATE note_shares SET revoked_at = now() WHERE note_id = @id AND revoked_at IS NULL", new { id });
        await audit.WriteAsync(ctx, "share.revoke", session, target: $"note:{id}", detail: $"{revoked} link(s)");
        return Results.Json(new { revoked });
    }

    // Public (no session). Fix: a link works only while it is neither revoked nor expired
    // (links without an expiry, e.g. created by an older build, count as expired).
    private static async Task<IResult> ReadSharedAsync(Database db, MarkdownRenderer markdown, string token)
    {
        await using var conn = await db.OpenConnectionAsync();
        var note = await conn.QueryFirstOrDefaultAsync<SharedNoteRow>(
            @"SELECT n.title, n.content, n.created_at, s.expires_at
              FROM note_shares s JOIN notes n ON n.id = s.note_id
              WHERE s.token = @token AND s.revoked_at IS NULL AND s.expires_at > now()",
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
