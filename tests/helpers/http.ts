import { NextRequest } from 'next/server';
import { POST as login } from '@/app/api/auth/login/route';

export const USERS = {
  a: { email: 'a@alpha.test', password: 'alpha-pass-123' },
  b: { email: 'b@beta.test', password: 'beta-pass-123' },
  admin: { email: 'admin@alpha.test', password: 'admin-pass-123' },
} as const;

const BASE = 'http://localhost:3000';

export function loginRequest(fields: Record<string, string>) {
  const form = new FormData();
  for (const [k, v] of Object.entries(fields)) form.set(k, v);
  return new NextRequest(`${BASE}/api/auth/login`, { method: 'POST', body: form });
}

/** Logs in through the real login route handler and returns the Cookie header to reuse. */
export async function loginAs(user: keyof typeof USERS): Promise<string> {
  const res = await login(loginRequest({ ...USERS[user] }));
  const session = res.cookies.get('session')?.value;
  if (!session) throw new Error(`login failed for ${user}`);
  return `session=${session}`;
}

export function req(path: string, init: { cookie?: string; method?: string; json?: unknown } = {}) {
  const headers: Record<string, string> = {};
  if (init.cookie) headers.cookie = init.cookie;
  if (init.json !== undefined) headers['content-type'] = 'application/json';
  return new NextRequest(`${BASE}${path}`, {
    method: init.method ?? 'GET',
    headers,
    body: init.json === undefined ? undefined : JSON.stringify(init.json),
  });
}

export const ctx = (id: number | string) => ({ params: Promise.resolve({ id: String(id) }) });
