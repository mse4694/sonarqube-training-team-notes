import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it } from 'vitest';
import { NoteBody } from '../app/notes/[id]/NoteBody';

describe('Lab 2 - stored XSS in note body', () => {
  it('escapes an <img onerror> payload instead of emitting it as HTML', () => {
    const html = renderToStaticMarkup(<NoteBody content={'<img src=x onerror=alert(1)>'} />);
    expect(html).not.toContain('<img');
    expect(html).toContain('&lt;img');
  });

  it('escapes <script> tags', () => {
    const html = renderToStaticMarkup(<NoteBody content={'<script>alert(1)</script>'} />);
    expect(html).not.toContain('<script>');
  });
});
