import Link from 'next/link';
import { notFound, redirect } from 'next/navigation';
import { getSession } from '@/lib/auth';
import { query } from '@/lib/db';
import { NoteBody } from './NoteBody';

type Props = { params: Promise<{ id: string }> };
type NoteRow = { id: number; title: string; content: string; attachment: string | null };

export default async function NotePage({ params }: Props) {
  const { id } = await params;
  const session = await getSession();
  if (!session) redirect(`/login?next=/notes/${id}`);

  const { rows } = await query<NoteRow>('SELECT id, title, content, attachment FROM notes WHERE id = $1', [Number(id)]);
  const note = rows[0];
  if (!note) notFound();

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
