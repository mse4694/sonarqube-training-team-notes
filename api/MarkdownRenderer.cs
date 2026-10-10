using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace TeamNotes.Api;

/// <summary>
/// Renders note content written in Markdown to HTML with Markdig (tables, task lists, autolinks...).
/// The HTML is sent to the browser, which inserts it as-is (web/components/MarkdownBody.tsx), so the
/// output must be safe by construction.
/// </summary>
public sealed class MarkdownRenderer
{
    private static readonly string[] SafeSchemes = ["http", "https", "mailto"];

    // Fix (configure the library, do not trust its defaults):
    // - DisableHtml(): raw HTML blocks/inlines are emitted as escaped text, never as markup.
    // - pick extensions explicitly instead of UseAdvancedExtensions(), which also enables
    //   generic attributes ({onmouseover=...} -> a real HTML attribute).
    // - links/images keep only http(s)/mailto or relative URLs (see SanitizeUrls).
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UsePipeTables()
        .UseTaskLists()
        .UseEmphasisExtras()
        .UseAutoLinks()
        .UseAutoIdentifiers()
        .DisableHtml()
        .Build();

    public string ToHtml(string markdown)
    {
        MarkdownDocument document = Markdown.Parse(markdown ?? "", Pipeline);
        SanitizeUrls(document);
        return document.ToHtml(Pipeline);
    }

    private static void SanitizeUrls(MarkdownDocument document)
    {
        foreach (var link in document.Descendants<LinkInline>())
        {
            if (!IsSafeUrl(link.Url))
            {
                link.Url = "#";
            }
        }
        foreach (var autolink in document.Descendants<AutolinkInline>())
        {
            if (!autolink.IsEmail && !IsSafeUrl(autolink.Url))
            {
                autolink.Url = "#";
            }
        }
    }

    /// <summary>Relative URLs and http/https/mailto pass; javascript:, data:, vbscript: ... do not.</summary>
    internal static bool IsSafeUrl(string? url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return true;
        }

        // Browsers ignore whitespace/control characters inside a scheme ("java\tscript:").
        string compact = new([.. url.Where(c => c > ' ')]);
        int colon = compact.IndexOf(':');
        if (colon < 0)
        {
            return true; // no scheme -> relative
        }
        int firstDelimiter = compact.IndexOfAny(['/', '?', '#']);
        if (firstDelimiter >= 0 && firstDelimiter < colon)
        {
            return true; // the ':' is in the path/query, e.g. "/notes?at=10:30"
        }
        string scheme = compact[..colon];
        return SafeSchemes.Contains(scheme, StringComparer.OrdinalIgnoreCase);
    }
}
