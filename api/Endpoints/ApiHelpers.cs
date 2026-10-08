namespace TeamNotes.Api.Endpoints;

public static class ApiHelpers
{
    public static Session? GetSession(HttpContext ctx) => ctx.Items["session"] as Session;

    public static IResult Unauthorized() => Results.Json(new { error = "unauthorized" }, statusCode: 401);

    /// <summary>Writes a 303 redirect (relative Location works behind Docker / proxies) + queued cookies.</summary>
    public static IResult SeeOther(string location) => Results.Extensions.SeeOther(location);
}

public static class ResultExtensions
{
    public static IResult SeeOther(this IResultExtensions _, string location)
        => new SeeOtherResult(location);

    private sealed class SeeOtherResult(string location) : IResult
    {
        public Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.StatusCode = StatusCodes.Status303SeeOther;
            httpContext.Response.Headers.Location = location;
            return Task.CompletedTask;
        }
    }
}
