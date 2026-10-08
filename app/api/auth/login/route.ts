import { NextResponse, type NextRequest } from 'next/server';
import {
  REMEMBER_COOKIE,
  SESSION_COOKIE,
  createRememberToken,
  createSessionToken,
  verifyPassword,
} from '@/lib/auth';
import { query } from '@/lib/db';

type UserRow = { id: number; email: string; team_id: number; role: 'member' | 'admin'; password_hash: string };

// Relative Location header: keeps the browser on the host it used (works behind Docker / proxies).
const redirectTo = (location: string) => new NextResponse(null, { status: 303, headers: { location } });

export async function POST(req: NextRequest) {
  const form = await req.formData();
  const email = String(form.get('email') ?? '');
  const password = String(form.get('password') ?? '');
  const next = String(form.get('next') || '/notes');

  const { rows } = await query<UserRow>(
    'SELECT id, email, team_id, role, password_hash FROM users WHERE email = $1',
    [email],
  );
  const user = rows[0];
  if (!user || !(await verifyPassword(password, user.password_hash))) {
    return redirectTo('/login?error=1');
  }

  const token = await createSessionToken({ userId: user.id, teamId: user.team_id, role: user.role, email: user.email });
  // Send the user back to the page they came from.
  const res = redirectTo(next);
  // The header component reads this cookie in the browser to show who is logged in.
  res.cookies.set(SESSION_COOKIE, token, { path: '/', httpOnly: false });
  if (form.get('remember') === 'on') {
    res.cookies.set(REMEMBER_COOKIE, createRememberToken(user.id), { path: '/', maxAge: 60 * 60 * 24 * 30 });
  }
  return res;
}
