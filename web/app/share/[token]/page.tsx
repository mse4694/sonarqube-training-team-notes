import { notFound } from 'next/navigation';
import { apiGet } from '@/lib/api';
import { expiryLabel } from '@/lib/share';
import { MarkdownBody } from '@/components/MarkdownBody';

type Props = { params: Promise<{ token: string }> };

export const dynamic = 'force-dynamic';

// Public page: no sign-in. Shows a note that its author shared with a link.
export default async function SharedNotePage({ params }: Props) {
  const { token } = await params;
  const res = await apiGet(`/api/share/${encodeURIComponent(token)}`);
  if (!res.ok) notFound();
  const note = (await res.json()).note as { title: string; content_html: string; expires_at: string | null };

  return (
    <main>
      <div className="card note-detail">
        <p className="shared-banner">
          <span className="badge badge-active">Shared note</span>
          <span className="share-meta">{expiryLabel(note.expires_at)}</span>
        </p>
        <h2>{note.title}</h2>
        <MarkdownBody html={note.content_html} />
      </div>
    </main>
  );
}
