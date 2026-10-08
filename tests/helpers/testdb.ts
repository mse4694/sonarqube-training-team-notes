import { readFileSync } from 'node:fs';
import { PGlite } from '@electric-sql/pglite';
import { setQueryImplementation, type Row } from '@/lib/db';

const INIT_SQL = readFileSync(new URL('../../db/init.sql', import.meta.url), 'utf8');

/** Fresh in-memory Postgres (PGlite = real Postgres compiled to WASM) seeded with db/init.sql. */
export async function freshDatabase() {
  const db = new PGlite();
  await db.exec(INIT_SQL);
  setQueryImplementation(async (text, params = []) => {
    // Multi-statement strings cannot be parameterised; run them with exec() like a stacked query.
    if (params.length === 0 && text.includes(';')) {
      const results = await db.exec(text);
      return { rows: (results.at(-1)?.rows ?? []) as Row[] };
    }
    const result = await db.query(text, params);
    return { rows: result.rows as Row[] };
  });
  return db;
}
