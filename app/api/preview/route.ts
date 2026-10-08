import { NextResponse, type NextRequest } from 'next/server';
import { getSessionFromRequest } from '@/lib/auth';

// "Link preview": fetch the page a user pasted into a note and return its <title>.
export async function GET(req: NextRequest) {
  const session = await getSessionFromRequest(req);
  if (!session) return NextResponse.json({ error: 'unauthorized' }, { status: 401 });

  const url = req.nextUrl.searchParams.get('url');
  if (!url) return NextResponse.json({ error: 'url is required' }, { status: 400 });
  try {
    const res = await fetch(url);
    const html = await res.text();
    const title = /<title>([^<]*)<\/title>/i.exec(html)?.[1] ?? null;
    return NextResponse.json({ url, status: res.status, title, snippet: html.slice(0, 300) });
  } catch {
    return NextResponse.json({ error: 'could not fetch url' }, { status: 502 });
  }
}
