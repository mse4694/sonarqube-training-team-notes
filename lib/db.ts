import pg from 'pg';
import { config } from './config';

export type Row = Record<string, unknown>;
export type QueryFn = (text: string, params?: unknown[]) => Promise<{ rows: Row[] }>;

let pool: pg.Pool | undefined;
let override: QueryFn | undefined;

/** Test seam: route queries to an in-memory Postgres (PGlite) in unit tests. */
export function setQueryImplementation(fn: QueryFn | undefined) {
  override = fn;
}

export async function query<T extends Row = Row>(text: string, params: unknown[] = []): Promise<{ rows: T[] }> {
  if (override) return (await override(text, params)) as { rows: T[] };
  pool ??= new pg.Pool({ connectionString: config.databaseUrl, max: 5 });
  const result = await pool.query(text, params);
  return { rows: result.rows as T[] };
}

/** Generic "UPDATE <table> SET <fields> WHERE id = $1" helper. */
export async function updateRow(table: 'notes' | 'users', id: number, fields: Row) {
  const keys = Object.keys(fields);
  if (keys.length === 0) return null;
  const sets = keys.map((key, i) => `${pg.escapeIdentifier(key)} = $${i + 2}`).join(', ');
  const values = keys.map((key) => fields[key]);
  const { rows } = await query(`UPDATE ${table} SET ${sets} WHERE id = $1 RETURNING id`, [id, ...values]);
  return rows[0] ?? null;
}
