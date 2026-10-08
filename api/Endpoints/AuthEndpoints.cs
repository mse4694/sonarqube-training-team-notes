using Dapper;

namespace TeamNotes.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuth(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/login", LoginAsync);
        app.MapPost("/api/auth/logout", Logout);
    }

    private static async Task<IResult> LoginAsync(HttpContext ctx, Database db, AuthService auth)
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

        if (user is null || !auth.VerifyPassword(password, user.password_hash))
        {
            return ApiHelpers.SeeOther("/login?error=1");
        }

        string token = auth.CreateSessionToken(new Session(user.id, user.team_id, user.role, user.email));

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
