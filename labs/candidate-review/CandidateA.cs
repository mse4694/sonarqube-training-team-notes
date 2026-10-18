using Markdig;
namespace CandidateAudit;
public sealed class CandidateA
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions().DisableHtml().Build();
    public string ToHtml(string markdown) => Markdown.ToHtml(markdown ?? "", Pipeline);
}
