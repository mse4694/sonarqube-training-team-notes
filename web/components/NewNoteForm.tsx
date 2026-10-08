'use client';

import { useState } from 'react';

export function NewNoteForm() {
  const [title, setTitle] = useState('');
  const [content, setContent] = useState('');

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    await fetch('/api/notes', {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({ title, content }),
    });
    window.location.reload();
  }

  return (
    <form onSubmit={submit} style={{ display: 'grid', gap: '0.5rem' }}>
      <h3>New note</h3>
      <input value={title} onChange={(e) => setTitle(e.target.value)} placeholder="Title" required />
      <textarea value={content} onChange={(e) => setContent(e.target.value)} rows={4} placeholder="Content (basic HTML allowed)" />
      <button type="submit">Create</button>
    </form>
  );
}
