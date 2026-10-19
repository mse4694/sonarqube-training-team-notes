namespace AgentControl;

/// <summary>
/// realpath-style resolution: every component must exist, every symbolic link (or junction)
/// on the way is followed, and ".." is applied to the already-resolved parent, so
/// "link/../x" means "the real parent of link's target", as the operating system reads it.
/// </summary>
public static class PathResolver
{
    private const int MaxLinks = 40;

    public static string ResolveStrict(string path)
    {
        var links = 0;
        return Resolve(path, ref links);
    }

    public static bool IsSameOrInside(string path, string root) =>
        path == root || path.StartsWith(root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar, StringComparison.Ordinal);

    private static string Resolve(string path, ref int links)
    {
        if (!Path.IsPathFullyQualified(path))
            throw new IOException("path must be absolute");
        var root = Path.GetPathRoot(path)!;
        var current = root;
        var parts = path[root.Length..].Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            if (part == ".")
                continue;
            if (part == "..")
            {
                current = Path.GetDirectoryName(current) ?? current;
                continue;
            }
            var candidate = Path.Combine(current, part);
            var info = new FileInfo(candidate);
            if (info.LinkTarget is { } target)
            {
                if (++links > MaxLinks)
                    throw new IOException("too many symbolic links");
                var next = Path.IsPathFullyQualified(target) ? target : Path.Combine(current, target);
                current = Resolve(next, ref links);
                continue;
            }
            if (!info.Exists && !Directory.Exists(candidate))
                throw new FileNotFoundException("path component does not exist");
            current = candidate;
        }
        return current;
    }
}
