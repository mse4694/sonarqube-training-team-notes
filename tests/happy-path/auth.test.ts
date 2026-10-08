import { beforeAll, describe, expect, it } from 'vitest';
import { POST as login } from '@/app/api/auth/login/route';
import { GET as me } from '@/app/api/me/route';
import { hashPassword, verifyPassword } from '@/lib/auth';
import { freshDatabase } from '../helpers/testdb';
import { USERS, loginAs, loginRequest, req } from '../helpers/http';

beforeAll(freshDatabase);

describe('login (happy path)', () => {
  it('redirects to /notes and sets a session cookie for valid credentials', async () => {
    const res = await login(loginRequest({ ...USERS.a }));
    expect(res.status).toBe(303);
    expect(res.headers.get('location')).toBe('/notes');
    expect(res.cookies.get('session')?.value).toBeTruthy();
  });

  it('keeps a relative ?next= target', async () => {
    const res = await login(loginRequest({ ...USERS.a, next: '/notes/2' }));
    expect(res.headers.get('location')).toBe('/notes/2');
  });

  it('rejects a wrong password', async () => {
    const res = await login(loginRequest({ email: USERS.a.email, password: 'nope' }));
    expect(res.headers.get('location')).toContain('/login?error=1');
    expect(res.cookies.get('session')).toBeUndefined();
  });

  it('GET /api/me returns the signed-in user', async () => {
    const cookie = await loginAs('b');
    const res = await me(req('/api/me', { cookie }));
    expect(res.status).toBe(200);
    expect((await res.json()).user.email).toBe('b@beta.test');
  });

  it('GET /api/me without a cookie is 401', async () => {
    expect((await me(req('/api/me'))).status).toBe(401);
  });

  it('password hashes verify', async () => {
    const hash = await hashPassword('s3cret!');
    expect(await verifyPassword('s3cret!', hash)).toBe(true);
    expect(await verifyPassword('wrong', hash)).toBe(false);
  });
});
