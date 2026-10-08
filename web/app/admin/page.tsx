import { redirect } from 'next/navigation';
import { apiGet } from '@/lib/api';

export const dynamic = 'force-dynamic';

type AdminUser = { id: number; email: string; role: string; team: string };

export default async function AdminPage() {
  const res = await apiGet('/api/admin/users');
  if (res.status === 401) redirect('/login?next=/admin');
  if (res.status === 403) redirect('/notes');
  const users = ((await res.json()).users ?? []) as AdminUser[];

  return (
    <main>
      <h2>Admin - users</h2>
      <ul>
        {users.map((u) => (
          <li key={u.id}>
            {u.email} ({u.role}) - {u.team}
          </li>
        ))}
      </ul>
    </main>
  );
}
