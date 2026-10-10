using Dapper;

namespace TeamNotes.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuth(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/login", LoginAsync);
        app.MapPost("/api/auth/logout", Logout);
    }

    private static async Task<IResult> LoginAsync(HttpContext ctx, Database db, AuthService auth, ILoggerFactory loggers)
    {
        var form = await ctx.Request.ReadFormAsync();
        string email = form["email"].ToString();
        string password = form["password"].ToString();
        // Lab 2 open redirect: "next" is sent straight to the Location header, no same-site check.
        string next = string.IsNullOrEmpty(form["next"].ToString()) ? "/notes" : form["next"].ToString();

        await using var conn = await db.OpenConnectionAsync();
        var user = await conn.QueryFirstOrDefaultAsync<UserRow>(
            "SELECT id, email, display_name, password_hash, role, team_id FROM users WHERE email = @email",
            new { email });

        // Lab 7 logging: "debug" log lines left in production write the typed password and the
        // issued session token into the application log, and nothing is recorded in audit_log,
        // so an attack leaves no security trail while the logs themselves leak credentials.
        var log = loggers.CreateLogger("TeamNotes.Auth");
        if (user is null || !auth.VerifyPassword(password, user.password_hash))
        {
            log.LogWarning("Login failed for {Email} with password {Password}", email, password);
            return ApiHelpers.SeeOther("/login?error=1");
        }

        string token = auth.CreateSessionToken(new Session(user.id, user.team_id, user.role, user.email));
        log.LogInformation("Login ok for {Email}, session token {Token}", email, token);

        // Day 1 vuln: the session cookie is not HttpOnly, not Secure and has no SameSite, so
        // client-side JS (an XSS payload) can read it and it rides along on cross-site requests.
        ctx.Response.Cookies.Append(AuthService.SessionCookie, token, new CookieOptions
        {
            Path = "/",
            HttpOnly = false,
        });
        return ApiHelpers.SeeOther(next);
    }

    private static IResult Logout(HttpContext ctx)
    {
        ctx.Response.Cookies.Delete(AuthService.SessionCookie);
        return ApiHelpers.SeeOther("/login");
    }
}
