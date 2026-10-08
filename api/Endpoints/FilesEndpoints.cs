namespace TeamNotes.Api.Endpoints;

public static class FilesEndpoints
{
    public static string UploadDir =>
        Environment.GetEnvironmentVariable("UPLOADS_DIR") is { Length: > 0 } v
            ? v
            : Path.Combine(Directory.GetCurrentDirectory(), "uploads");

    public static void MapFiles(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/files", DownloadAsync);
    }

    // Fix: resolve the final absolute path and confirm it is still inside uploads/
    // (blocks "../" traversal and absolute paths like /etc/passwd).
    private static async Task<IResult> DownloadAsync(HttpContext ctx, string? name)
    {
        var session = ApiHelpers.GetSession(ctx);
        if (session is null) return ApiHelpers.Unauthorized();

        name ??= "";
        string root = Path.GetFullPath(UploadDir);
        string filePath = Path.GetFullPath(Path.Combine(root, name));
        if (string.IsNullOrEmpty(name) || !filePath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            return Results.Json(new { error = "invalid file name" }, statusCode: 400);
        }
        try
        {
            byte[] data = await File.ReadAllBytesAsync(filePath);
            return Results.File(data, "application/octet-stream", Path.GetFileName(name));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Results.Json(new { error = "file not found" }, statusCode: 404);
        }
    }
}
