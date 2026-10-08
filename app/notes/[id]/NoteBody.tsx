// Notes support simple rich text (<b>, <i>, links), so we render the stored HTML.
export function NoteBody({ content }: { content: string }) {
  return <article className="note-body" dangerouslySetInnerHTML={{ __html: content }} />;
}
