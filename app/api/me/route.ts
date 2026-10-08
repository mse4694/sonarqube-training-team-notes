import { NextResponse, type NextRequest } from 'next/server';
import { getSessionFromRequest } from '@/lib/auth';
import { query, updateRow } from '@/lib/db';

const ME_SQL = 'SELECT id, email, display_name, role, team_id FROM users WHERE id = $1';

export async function GET(req: NextRequest) {
  const session = await getSessionFromRequest(req);
  if (!session) return NextResponse.json({ error: 'unauthorized' }, { status: 401 });
  const { rows } = await query(ME_SQL, [session.userId]);
  return NextResponse.json({ user: rows[0] });
}

// Profile update (display name).
export async function PATCH(req: NextRequest) {
  const session = await getSessionFromRequest(req);
  if (!session) return NextResponse.json({ error: 'unauthorized' }, { status: 401 });
  const body = await req.json();
  await updateRow('users', session.userId, { ...body });
  const { rows } = await query(ME_SQL, [session.userId]);
  return NextResponse.json({ user: rows[0] });
}
