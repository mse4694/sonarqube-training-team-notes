import { NextResponse, type NextRequest } from 'next/server';
import { getSessionFromRequest } from '@/lib/auth';
import { query } from '@/lib/db';

export async function GET(req: NextRequest) {
  const session = await getSessionFromRequest(req);
  if (!session) return NextResponse.json({ error: 'unauthorized' }, { status: 401 });
  const { rows } = await query(
    'SELECT id, title, owner_id, team_id, created_at FROM notes WHERE team_id = $1 ORDER BY id',
    [session.teamId],
  );
  return NextResponse.json({ notes: rows });
}

export async function POST(req: NextRequest) {
  const session = await getSessionFromRequest(req);
  if (!session) return NextResponse.json({ error: 'unauthorized' }, { status: 401 });
  const body = (await req.json()) as { title?: string; content?: string };
  if (!body.title) return NextResponse.json({ error: 'title is required' }, { status: 400 });
  const { rows } = await query(
    'INSERT INTO notes (team_id, owner_id, title, content) VALUES ($1, $2, $3, $4) RETURNING id, title, team_id',
    [session.teamId, session.userId, body.title, body.content ?? ''],
  );
  return NextResponse.json({ note: rows[0] }, { status: 201 });
}
