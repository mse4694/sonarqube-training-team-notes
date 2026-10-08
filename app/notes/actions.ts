'use server';

import { revalidatePath } from 'next/cache';
import { redirect } from 'next/navigation';
import { getSession } from '@/lib/auth';
import { query } from '@/lib/db';

export async function createNote(formData: FormData) {
  const session = await getSession();
  if (!session) redirect('/login');
  const title = String(formData.get('title') ?? '').trim();
  const content = String(formData.get('content') ?? '');
  if (!title) return;
  await query('INSERT INTO notes (team_id, owner_id, title, content) VALUES ($1, $2, $3, $4)', [
    session.teamId,
    session.userId,
    title,
    content,
  ]);
  revalidatePath('/notes');
}
