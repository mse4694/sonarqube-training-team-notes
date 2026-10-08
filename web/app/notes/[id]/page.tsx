import Link from 'next/link';
import { notFound, redirect } from 'next/navigation';
import { apiGet } from '@/lib/api';
import { NoteBody } from './NoteBody';

type Props = { params: Promise<{ id: string }> };

export const dynamic = 'force-dynamic';

export default async function NotePage({ params }: Props) {
  const { id } = await params;
  const res = await apiGet(`/api/notes/${id}`);
  if (res.status === 401) redirect(`/login?next=/notes/${id}`);
  if (res.status === 404) notFound();
  const note = (await res.json()).note as { title: string; content: string; attachment: string | null };

  return (
    <main>
      <Link href="/notes">&larr; All notes</Link>
      <h2>{note.title}</h2>
      <NoteBody content={note.content} />
      {note.attachment && (
        <p>
          Attachment: <a href={`/api/files?name=${encodeURIComponent(note.attachment)}`}>{note.attachment}</a>
        </p>
      )}
    </main>
  );
}
