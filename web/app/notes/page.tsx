import Link from 'next/link';
import { redirect } from 'next/navigation';
import { apiGet } from '@/lib/api';
import { NewNoteForm } from '@/components/NewNoteForm';
import { TokenStash } from '@/components/TokenStash';

export const dynamic = 'force-dynamic';

type Note = { id: number; title: string };
type Me = { email: string; role: string };

export default async function NotesPage() {
  const meRes = await apiGet('/api/me');
  if (meRes.status === 401) redirect('/login?next=/notes');
  const me = (await meRes.json()).user as Me;
  const notes = ((await (await apiGet('/api/notes')).json()).notes ?? []) as Note[];

  return (
    <main style={{ display: 'grid', gap: '1.5rem' }}>
      <TokenStash />
      <nav style={{ display: 'flex', gap: '1rem' }}>
        <span>Signed in as {me.email}</span>
        {me.role === 'admin' && <Link href="/admin">Admin</Link>}
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

      <NewNoteForm />

      <form method="get" action="/api/preview">
        <input name="url" placeholder="https://example.com" /> <button type="submit">Preview link</button>
      </form>
    </main>
  );
}
