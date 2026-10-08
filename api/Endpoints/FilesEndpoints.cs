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

    // Lab 2 path traversal: `name` is joined onto the uploads directory and opened directly.
    // Path.Combine does not strip "../", so name=../../../etc/passwd escapes the uploads folder.
    private static async Task<IResult> DownloadAsync(HttpContext ctx, string? name)
    {
        var session = ApiHelpers.GetSession(ctx);
        if (session is null) return ApiHelpers.Unauthorized();

        name ??= "";
        string filePath = Path.Combine(UploadDir, name);
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
