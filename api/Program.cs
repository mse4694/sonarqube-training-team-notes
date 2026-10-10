using TeamNotes.Api;
using TeamNotes.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(_ => new Database(AppConfig.DatabaseUrl));
builder.Services.AddSingleton<AuthService>();
builder.Services.AddSingleton<MarkdownRenderer>();
builder.Services.AddSingleton<ApiKeyService>();

// Link-preview client. redirects disabled so a public URL cannot 30x-bounce to an internal one.
builder.Services.AddHttpClient("preview", c => c.Timeout = TimeSpan.FromSeconds(5))
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false });

builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.SetIsOriginAllowed(_ => true).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

var app = builder.Build();

// Create schema + seed training data (retry while the DB container comes up).
await InitializeDatabaseAsync(app);

// Lab 7 error handling: the developer exception page is enabled unconditionally ("so support can
// see what broke"), so in Production any unhandled exception returns the exception type, message,
// stack trace and source paths to the caller. No security headers are sent either.
app.UseDeveloperExceptionPage();

app.UseCors();

// Session middleware: read the `session` cookie and stash the resolved Session on the request.
app.Use(async (ctx, next) =>
{
    string? token = ctx.Request.Cookies[AuthService.SessionCookie];
    if (!string.IsNullOrEmpty(token))
    {
        var auth = ctx.RequestServices.GetRequiredService<AuthService>();
        var session = auth.ValidateSessionToken(token);
        if (session is not null)
        {
            ctx.Items["session"] = session;
        }
    }

    // Scripts / CI can authenticate with a personal API key instead of the cookie.
    if (ctx.Items["session"] is null && ctx.Request.Headers[ApiKeyService.HeaderName].ToString() is { Length: > 0 } apiKey)
    {
        var keys = ctx.RequestServices.GetRequiredService<ApiKeyService>();
        var session = await keys.AuthenticateAsync(apiKey);
        if (session is not null)
        {
            ctx.Items["session"] = session;
        }
    }
    await next();
});

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapAuth();
app.MapNotes();
app.MapMe();
app.MapAdmin();
app.MapFiles();
app.MapPreview();
app.MapMarkdown();
app.MapShares();
app.MapApiKeys();

await app.RunAsync();

static async Task InitializeDatabaseAsync(WebApplication app)
{
    var db = app.Services.GetRequiredService<Database>();
    var auth = app.Services.GetRequiredService<AuthService>();
    for (int attempt = 1; ; attempt++)
    {
        try
        {
            await DbInitializer.EnsureCreatedAsync(db, auth);
            return;
        }
        catch (Exception ex) when (attempt < 30)
        {
            app.Logger.LogWarning("DB not ready (attempt {Attempt}): {Message}", attempt, ex.Message);
            await Task.Delay(1000);
        }
    }
}

// Exposed to the xUnit WebApplicationFactory.
public partial class Program { }
