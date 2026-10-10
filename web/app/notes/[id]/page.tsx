import Link from 'next/link';
import { notFound, redirect } from 'next/navigation';
import { apiGet, apiPost } from '@/lib/api';
import type { Share } from '@/lib/share';
import { MarkdownBody } from '@/components/MarkdownBody';
import { ShareCard } from '@/components/ShareCard';
import { NoteBody } from './NoteBody';

type Props = { params: Promise<{ id: string }>; searchParams: Promise<{ view?: string }> };

export const dynamic = 'force-dynamic';

export default async function NotePage({ params, searchParams }: Props) {
  const { id } = await params;
  const { view } = await searchParams;
  const res = await apiGet(`/api/notes/${id}`);
  if (res.status === 401) redirect(`/login?next=/notes/${id}`);
  if (res.status === 404) notFound();
  const note = (await res.json()).note as { id: number; title: string; content: string; attachment: string | null };

  const markdownView = view === 'markdown';
  const html = markdownView
    ? (((await (await apiPost('/api/markdown/render', { markdown: note.content })).json()).html ?? '') as string)
    : '';
  const sharesRes = await apiGet(`/api/notes/${id}/shares`);
  const shares = (sharesRes.ok ? ((await sharesRes.json()).shares ?? []) : []) as Share[];

  return (
    <main>
      <Link href="/notes" className="back-link">&larr; All notes</Link>
      <div className="card note-detail">
        <div className="note-detail-head">
          <h2>{note.title}</h2>
          <nav className="tabs" aria-label="Note view">
            <Link href={`/notes/${id}`} className={markdownView ? 'tab' : 'tab tab-active'}>
              Text
            </Link>
            <Link href={`/notes/${id}?view=markdown`} className={markdownView ? 'tab tab-active' : 'tab'}>
              Markdown
            </Link>
          </nav>
        </div>
        {markdownView ? <MarkdownBody html={html} /> : <NoteBody content={note.content} />}
        {note.attachment && (
          <p className="attachment">
            Attachment:{' '}
            <a href={`/api/files?name=${encodeURIComponent(note.attachment)}`} className="attachment-chip">
              {note.attachment}
            </a>
          </p>
        )}
      </div>
      <ShareCard noteId={note.id} initialShares={shares} />
    </main>
  );
}
