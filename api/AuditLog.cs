using Dapper;

namespace TeamNotes.Api;

/// <summary>
/// Security audit trail (OWASP A09): who did what to which object, from where, and when.
/// Rows go to the audit_log table (admin page /admin/audit) and a structured log event is emitted.
/// Never pass secrets here: no passwords, session tokens, API keys or share tokens.
/// </summary>
public sealed class AuditLog(Database db, ILogger<AuditLog> logger)
{
    private const int MaxFieldLength = 200;

    public async Task WriteAsync(HttpContext ctx, string action, Session? actor, string? target = null,
        string? detail = null, string? attemptedEmail = null)
    {
        string? ip = ctx.Connection.RemoteIpAddress?.ToString();
        int? actorId = actor?.UserId;
        string? actorEmail = Clip(actor?.Email ?? attemptedEmail);

        await using var conn = await db.OpenConnectionAsync();
        await conn.ExecuteAsync(
            "INSERT INTO audit_log (actor_id, actor_email, action, target, ip, detail) VALUES (@actorId, @actorEmail, @action, @target, @ip, @detail)",
            new { actorId, actorEmail, action, target = Clip(target), ip, detail = Clip(detail) });

        // Structured fields only; user-typed values (emails, names) stay out of the log line
        // so they cannot forge log entries.
        logger.LogInformation("audit {Action} actor={ActorId} target={Target} ip={Ip}", action, actorId, target, ip);
    }

    private static string? Clip(string? value) =>
        value is null ? null : value.Length <= MaxFieldLength ? value : value[..MaxFieldLength];
}
