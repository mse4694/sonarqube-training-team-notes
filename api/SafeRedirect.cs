namespace TeamNotes.Api;

public static class SafeRedirect
{
    /// <summary>Only same-site relative paths ("/notes/2"). Rejects "https://evil", "//evil", "/\evil", "javascript:".</summary>
    public static string SafeNextPath(string? next, string fallback = "/notes")
    {
        if (string.IsNullOrEmpty(next)
            || !next.StartsWith('/')
            || next.StartsWith("//", StringComparison.Ordinal)
            || next.StartsWith("/\\", StringComparison.Ordinal))
        {
            return fallback;
        }
        return next;
    }
}
