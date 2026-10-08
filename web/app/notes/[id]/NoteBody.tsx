'use client';

// Render note content as text: React escapes it, so a stored <script> / <img onerror> stays inert.
// (If rich text is needed later, sanitize with an allowlist such as DOMPurify before rendering HTML.)
export function NoteBody({ content }: Readonly<{ content: string }>) {
  return (
    <article className="note-body" style={{ whiteSpace: 'pre-wrap' }}>
      {content}
    </article>
  );
}
