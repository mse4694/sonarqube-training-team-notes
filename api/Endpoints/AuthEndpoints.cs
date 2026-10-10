using Dapper;

namespace TeamNotes.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuth(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/login", LoginAsync);
        app.MapPost("/api/auth/logout", Logout);
    }

    private static async Task<IResult> LoginAsync(HttpContext ctx, Database db, AuthService auth, LoginThrottle throttle, ILoggerFactory loggers)
    {
        var form = await ctx.Request.ReadFormAsync();
        string email = form["email"].ToString();
        string password = form["password"].ToString();
        // Fix: accept only same-site relative paths for the post-login redirect.
        string next = SafeRedirect.SafeNextPath(form["next"].ToString());

        // Fix: throttle online password guessing per client IP + email (fixed window, 429).
        string throttleKey = LoginThrottle.KeyFor(ctx, email);
        if (throttle.IsBlocked(throttleKey))
        {
            ctx.Response.Headers.RetryAfter = ((int)LoginThrottle.Window.TotalSeconds).ToString();
            return Results.Json(new { error = "too many failed sign-in attempts, try again in a minute" }, statusCode: 429);
        }

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
            throttle.RecordFailure(throttleKey);
            log.LogWarning("Login failed for {Email} with password {Password}", email, password);
            return ApiHelpers.SeeOther("/login?error=1");
        }

        string token = auth.CreateSessionToken(new Session(user.id, user.team_id, user.role, user.email));
        log.LogInformation("Login ok for {Email}, session token {Token}", email, token);

        // HttpOnly: not readable from JS (XSS cannot steal it). SameSite=Lax: not sent on
        // cross-site POSTs (CSRF). Secure in production; COOKIE_INSECURE=1 allows plain http in class.
        ctx.Response.Cookies.Append(AuthService.SessionCookie, token, new CookieOptions
        {
            Path = "/",
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = AppConfig.IsProduction && !AppConfig.CookieInsecure,
            MaxAge = TimeSpan.FromHours(8),
        });
        return ApiHelpers.SeeOther(next);
    }

    private static IResult Logout(HttpContext ctx)
    {
        ctx.Response.Cookies.Delete(AuthService.SessionCookie);
        return ApiHelpers.SeeOther("/login");
    }
}
