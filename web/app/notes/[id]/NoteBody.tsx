'use client';

// Notes support simple rich text (<b>, <i>, links), so we render the stored HTML.
// Lab 2 stored XSS: content comes from another user and is injected as raw HTML, so a note
// containing <img src=x onerror=...> or <script> runs in the reader's browser.
export function NoteBody({ content }: { content: string }) {
  return <article className="note-body" dangerouslySetInnerHTML={{ __html: content }} />;
}
