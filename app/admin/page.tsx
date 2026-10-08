import { redirect } from 'next/navigation';
import { getSession } from '@/lib/auth';
import { query } from '@/lib/db';

export const dynamic = 'force-dynamic';

export default async function AdminPage() {
  const session = await getSession();
  if (session?.role !== 'admin') redirect('/notes');
  const { rows } = await query<{ id: number; email: string; role: string }>('SELECT id, email, role FROM users ORDER BY id');
  return (
    <main>
      <h2>Admin - users</h2>
      <ul>
        {rows.map((u) => (
          <li key={u.id}>
            {u.email} ({u.role})
          </li>
        ))}
      </ul>
    </main>
  );
}
