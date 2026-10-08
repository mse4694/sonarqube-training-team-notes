import Link from 'next/link';
import { redirect } from 'next/navigation';
import { getSession } from '@/lib/auth';
import { query } from '@/lib/db';
import { createNote } from './actions';

export const dynamic = 'force-dynamic';

export default async function NotesPage() {
  const session = await getSession();
  if (!session) redirect('/login?next=/notes');
  const { rows: notes } = await query<{ id: number; title: string }>(
    'SELECT id, title FROM notes WHERE team_id = $1 ORDER BY id',
    [session.teamId],
  );

  return (
    <main style={{ display: 'grid', gap: '1.5rem' }}>
      <nav style={{ display: 'flex', gap: '1rem' }}>
        <span>Signed in as {session.email}</span>
        {session.role === 'admin' && <Link href="/admin">Admin</Link>}
        <form method="post" action="/api/auth/logout">
          <button type="submit">Sign out</button>
        </form>
      </nav>

      <form method="get" action="/api/notes/search">
        <input name="q" placeholder="Search notes" /> <button type="submit">Search</button>
      </form>

      <ul>
        {notes.map((n) => (
          <li key={n.id}>
            <Link href={`/notes/${n.id}`}>{n.title}</Link>
          </li>
        ))}
      </ul>

      <form action={createNote} style={{ display: 'grid', gap: '0.5rem' }}>
        <h3>New note</h3>
        <input name="title" placeholder="Title" required />
        <textarea name="content" rows={4} placeholder="Content (basic HTML allowed)" />
        <button type="submit">Create</button>
      </form>

      <form method="get" action="/api/preview">
        <input name="url" placeholder="https://example.com" /> <button type="submit">Preview link</button>
      </form>
    </main>
  );
}
