// Renders HTML that the API produced from Markdown (api/MarkdownRenderer.cs).
// The browser trusts whatever the server rendered, so the server-side Markdig configuration
// decides whether a note can inject markup here (Lab 5).
export function MarkdownBody({ html }: Readonly<{ html: string }>) {
  return <article className="note-body markdown-body" dangerouslySetInnerHTML={{ __html: html }} />;
}
