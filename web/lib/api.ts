import { cookies } from 'next/headers';

export const API_INTERNAL = process.env.API_INTERNAL_URL ?? 'http://api:8080';

/** Server-side GET to the API, forwarding the caller's session cookie. */
export async function apiGet(path: string): Promise<Response> {
  const cookieHeader = (await cookies()).toString();
  return fetch(`${API_INTERNAL}${path}`, {
    headers: cookieHeader ? { cookie: cookieHeader } : {},
    cache: 'no-store',
  });
}

/** Trim a snippet for display. Pure helper (unit-tested). */
export function truncate(text: string, max = 140): string {
  return text.length > max ? text.slice(0, max) + '…' : text;
}
