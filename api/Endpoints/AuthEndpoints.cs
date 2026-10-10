using Dapper;

namespace TeamNotes.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuth(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/login", LoginAsync);
        app.MapPost("/api/auth/logout", Logout);
    }

    private static async Task<IResult> LoginAsync(HttpContext ctx, Database db, AuthService auth, LoginThrottle throttle, AuditLog audit)
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
            await audit.WriteAsync(ctx, "auth.login.throttled", actor: null, attemptedEmail: email);
            ctx.Response.Headers.RetryAfter = ((int)LoginThrottle.Window.TotalSeconds).ToString();
            return Results.Json(new { error = "too many failed sign-in attempts, try again in a minute" }, statusCode: 429);
        }

        await using var conn = await db.OpenConnectionAsync();
        var user = await conn.QueryFirstOrDefaultAsync<UserRow>(
            "SELECT id, email, display_name, password_hash, role, team_id FROM users WHERE email = @email",
            new { email });

        // Fix: failures and successes go to the audit trail; the password and the session token
        // are never written anywhere.
        if (user is null || !auth.VerifyPassword(password, user.password_hash))
        {
            throttle.RecordFailure(throttleKey);
            await audit.WriteAsync(ctx, "auth.login.failure", actor: null, detail: "wrong email or password", attemptedEmail: email);
            return ApiHelpers.SeeOther("/login?error=1");
        }

        var session = new Session(user.id, user.team_id, user.role, user.email);
        string token = auth.CreateSessionToken(session);
        await audit.WriteAsync(ctx, "auth.login.success", session, target: $"user:{user.id}");

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
