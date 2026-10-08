import { readFile } from 'node:fs/promises';
import path from 'node:path';
import { NextResponse, type NextRequest } from 'next/server';
import { getSessionFromRequest } from '@/lib/auth';

const UPLOAD_DIR = path.join(process.cwd(), 'uploads');

export async function GET(req: NextRequest) {
  const session = await getSessionFromRequest(req);
  if (!session) return NextResponse.json({ error: 'unauthorized' }, { status: 401 });

  const name = req.nextUrl.searchParams.get('name') ?? '';
  try {
    const data = await readFile(path.join(UPLOAD_DIR, name));
    return new NextResponse(data, {
      headers: {
        'content-type': 'application/octet-stream',
        'content-disposition': `attachment; filename="${path.basename(name)}"`,
      },
    });
  } catch {
    return NextResponse.json({ error: 'file not found' }, { status: 404 });
  }
}
