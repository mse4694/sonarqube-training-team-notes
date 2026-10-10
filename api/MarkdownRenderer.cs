using Markdig;

namespace TeamNotes.Api;

/// <summary>
/// Renders note content written in Markdown to HTML with Markdig (tables, task lists, autolinks...).
/// The HTML is sent to the browser, which inserts it as-is (web/components/MarkdownBody.tsx).
/// </summary>
public sealed class MarkdownRenderer
{
    // Lab 5 Markdown XSS: the default Markdig pipeline passes raw HTML blocks/inlines straight
    // through and keeps any link scheme, so "<img src=x onerror=...>" or "[x](javascript:...)"
    // in a note becomes live HTML. The taint (note content) flows source -> Markdig -> sink
    // (dangerouslySetInnerHTML) through a third-party library, which is what Advanced SAST tracks.
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    public string ToHtml(string markdown) => Markdown.ToHtml(markdown ?? "", Pipeline);
}
