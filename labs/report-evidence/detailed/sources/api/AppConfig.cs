namespace TeamNotes.Api;

/// <summary>
/// Central application configuration.
/// NOTE (training app): every secret below is FAKE and exists only for the SonarQube labs.
/// </summary>
public static class AppConfig
{
    // Falls back to the shared staging database when DATABASE_URL is not set.
    // (Lab 2 secrets: a PostgreSQL password hard-coded in the source.)
    public const string FallbackDatabaseUrl =
        "postgres://notes:[REDACTED]@db.staging.internal:5432/notes";

    // Used to sign the session JWT (HS256). Hard-coded signing key.
    public const string JwtSecret = "[REDACTED]";

    // Attachment storage (S3) - TODO: move to env vars before go-live
    public const string AwsRegion = "ap-southeast-1";
    public const string AwsBucket = "team-notes-attachments";
    public const string AwsAccessKeyId = "[REDACTED]";
    public const string AwsSecretAccessKey = "[REDACTED]";

    public static string DatabaseUrl =>
        Environment.GetEnvironmentVariable("DATABASE_URL") is { Length: > 0 } v ? v : FallbackDatabaseUrl;

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

