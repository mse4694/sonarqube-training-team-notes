import Link from 'next/link';
import { redirect } from 'next/navigation';
import { apiGet } from '@/lib/api';
import { NewNoteForm } from '@/components/NewNoteForm';
import { TokenStash } from '@/components/TokenStash';
import { UserNav, type Me } from '@/components/UserNav';

export const dynamic = 'force-dynamic';

type Note = { id: number; title: string };

export default async function NotesPage() {
  const meRes = await apiGet('/api/me');
  if (meRes.status === 401) redirect('/login?next=/notes');
  const me = (await meRes.json()).user as Me;
  const notes = ((await (await apiGet('/api/notes')).json()).notes ?? []) as Note[];

  return (
    <main>
      <TokenStash />
      <UserNav me={me} />

      <div className="page-head">
        <div>
          <h2>Notes</h2>
          <p className="page-sub">Everything your team has written down, in one place.</p>
        </div>
      </div>

      <div className="notes-layout">
        <section className="notes-main">
          <form method="get" action="/api/notes/search" className="search-bar">
            <input name="q" placeholder="Search notes" /> <button type="submit">Search</button>
          </form>

          <ul className="note-list">
            {notes.map((n) => (
              <li key={n.id} className="note-item">
                <span className="note-icon" aria-hidden="true">
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                    <path d="M14 3H7a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V8z" />
                    <path d="M14 3v5h5" />
                    <path d="M9 13h6M9 17h4" />
                  </svg>
                </span>
                <span className="note-text">
                  <Link href={`/notes/${n.id}`} className="note-link">
                    {n.title}
                  </Link>
                  <span className="note-meta">Note #{n.id}</span>
                </span>
                <span className="note-chevron" aria-hidden="true">&rsaquo;</span>
              </li>
            ))}
          </ul>
        </section>

        <aside className="side">
          <NewNoteForm />

          <form method="get" action="/api/preview" className="card form-card">
            <h3>Link preview</h3>
            <p className="card-sub">Fetch a page title for a URL.</p>
            <input name="url" placeholder="https://example.com" /> <button type="submit">Preview link</button>
          </form>
        </aside>
      </div>
    </main>
  );
}
