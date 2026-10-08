import { NextResponse, type NextRequest } from 'next/server';
import { getSessionFromRequest } from '@/lib/auth';
import { query } from '@/lib/db';

export async function GET(req: NextRequest) {
  const session = await getSessionFromRequest(req);
  if (!session) return NextResponse.json({ error: 'unauthorized' }, { status: 401 });

  const q = req.nextUrl.searchParams.get('q') ?? '';
  const sql =
    `SELECT id, title, team_id FROM notes WHERE team_id = ${session.teamId} ` +
    `AND (title ILIKE '%${q}%' OR content ILIKE '%${q}%') ORDER BY id`;
  const { rows } = await query(sql);
  return NextResponse.json({ results: rows });
}
