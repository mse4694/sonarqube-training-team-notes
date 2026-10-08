import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it } from 'vitest';
import { NoteBody } from '@/app/notes/[id]/NoteBody';

describe('NoteBody (happy path)', () => {
  it('shows the note text', () => {
    expect(renderToStaticMarkup(<NoteBody content="Ship search on Friday" />)).toContain('Ship search on Friday');
  });
});
