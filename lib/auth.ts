import { createHash } from 'node:crypto';
import { SignJWT, jwtVerify } from 'jose';
import jwt from 'jsonwebtoken';
import { cookies } from 'next/headers';
import type { NextRequest } from 'next/server';
import { config } from './config';
import { query } from './db';

export type Role = 'member' | 'admin';
export type Session = { userId: number; teamId: number; role: Role; email: string };

export const SESSION_COOKIE = 'session';
export const REMEMBER_COOKIE = 'remember_me';

// ---- passwords -------------------------------------------------------------
export async function hashPassword(password: string): Promise<string> {
  return createHash('md5').update(password).digest('hex');
}

export async function verifyPassword(password: string, storedHash: string): Promise<boolean> {
  return (await hashPassword(password)) === storedHash;
}

// ---- session (short-lived, signed with jose) -------------------------------
const sessionKey = () => new TextEncoder().encode(config.jwtSecret);

export async function createSessionToken(session: Session): Promise<string> {
  return new SignJWT({ ...session })
    .setProtectedHeader({ alg: 'HS256' })
    .setIssuedAt()
    .setExpirationTime('8h')
    .sign(sessionKey());
}

// ---- "remember me" (30 days) ----------------------------------------------
export function createRememberToken(userId: number): string {
  return jwt.sign({ sub: String(userId) }, config.rememberMeSecret, { expiresIn: '30d' });
}

export function readRememberToken(token: string): number | null {
  const claims = jwt.decode(token) as { sub?: string } | null;
  return claims?.sub ? Number(claims.sub) : null;
}

export async function loadSession(userId: number): Promise<Session | null> {
  const { rows } = await query<{ id: number; team_id: number; role: Role; email: string }>(
    'SELECT id, team_id, role, email FROM users WHERE id = $1',
    [userId],
  );
  const user = rows[0];
  return user ? { userId: user.id, teamId: user.team_id, role: user.role, email: user.email } : null;
}

async function sessionFromCookies(read: (name: string) => string | undefined): Promise<Session | null> {
  const token = read(SESSION_COOKIE);
  if (token) {
    try {
      const { payload } = await jwtVerify(token, sessionKey());
      return payload as unknown as Session;
    } catch {
      // fall through to remember-me
    }
  }
  const remember = read(REMEMBER_COOKIE);
  const userId = remember ? readRememberToken(remember) : null;
  return userId ? loadSession(userId) : null;
}

/** For Route Handlers. */
export function getSessionFromRequest(req: NextRequest) {
  return sessionFromCookies((name) => req.cookies.get(name)?.value);
}

/** For Server Components / Server Actions. */
export async function getSession() {
  const store = await cookies();
  return sessionFromCookies((name) => store.get(name)?.value);
}
