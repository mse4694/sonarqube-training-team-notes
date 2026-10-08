namespace TeamNotes.Api;

/// <summary>
/// Central application configuration. Secrets come ONLY from the environment (see .env.example);
/// in production inject them from a secret manager. Nothing sensitive is hard-coded.
/// </summary>
public static class AppConfig
{
    // No hard-coded fallback: a missing DATABASE_URL must fail fast, not silently use a shared DB.
    public const string FallbackDatabaseUrl = "";

    public static string DatabaseUrl => RequireEnv("DATABASE_URL");

    // Used to sign the session JWT (HS256).
    public static string JwtSecret => RequireEnv("SESSION_SECRET");

    public static string AwsRegion => Environment.GetEnvironmentVariable("AWS_REGION") ?? "ap-southeast-1";
    public static string AwsBucket => Environment.GetEnvironmentVariable("S3_BUCKET") ?? "team-notes-attachments";
    public static string AwsAccessKeyId => Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID") ?? "";
    public static string AwsSecretAccessKey => Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY") ?? "";

    private static string RequireEnv(string name) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } v
            ? v
            : throw new InvalidOperationException($"Missing required environment variable {name} (see .env.example)");

    /// <summary>Session cookies are Secure in production; set COOKIE_INSECURE=1 for plain http in class.</summary>
    public static bool CookieInsecure =>
        Environment.GetEnvironmentVariable("COOKIE_INSECURE") == "1";

    public static bool IsProduction =>
        (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production") == "Production";

    /// <summary>Accepts a postgres:// URI or a native Npgsql key=value string and returns Npgsql form.</summary>
    public static string NpgsqlConnectionString(string raw)
    {
        if (!raw.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
            !raw.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            return raw; // already key=value
        }

        var uri = new Uri(raw);
        var userInfo = uri.UserInfo.Split(':', 2);
        var builder = new Npgsql.NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Database = uri.AbsolutePath.TrimStart('/'),
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "",
        };
        return builder.ConnectionString;
    }
}
