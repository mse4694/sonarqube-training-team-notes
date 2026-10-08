import { NextResponse } from 'next/server';
import { REMEMBER_COOKIE, SESSION_COOKIE } from '@/lib/auth';

export async function POST() {
  const res = new NextResponse(null, { status: 303, headers: { location: '/login' } });
  res.cookies.delete(SESSION_COOKIE);
  res.cookies.delete(REMEMBER_COOKIE);
  return res;
}
