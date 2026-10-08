import { NextResponse, type NextRequest } from 'next/server';
import { getSessionFromRequest } from '@/lib/auth';
import { query } from '@/lib/db';

// Admin console data. The "Admin" link is only rendered for admins (see app/notes/page.tsx).
export async function GET(req: NextRequest) {
  const session = await getSessionFromRequest(req);
  if (!session) return NextResponse.json({ error: 'unauthorized' }, { status: 401 });
  const { rows } = await query(
    'SELECT u.id, u.email, u.display_name, u.role, t.slug AS team FROM users u JOIN teams t ON t.id = u.team_id ORDER BY u.id',
  );
  return NextResponse.json({ users: rows });
}
