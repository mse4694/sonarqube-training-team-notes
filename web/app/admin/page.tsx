import { redirect } from 'next/navigation';
import Link from 'next/link';
import { apiGet } from '@/lib/api';
import { UserNav, type Me } from '@/components/UserNav';

export const dynamic = 'force-dynamic';

type AdminUser = { id: number; email: string; role: string; team: string };

export default async function AdminPage() {
  const res = await apiGet('/api/admin/users');
  if (res.status === 401) redirect('/login?next=/admin');
  if (res.status === 403) redirect('/notes');
  const users = ((await res.json()).users ?? []) as AdminUser[];
  const me = (await (await apiGet('/api/me')).json()).user as Me;

  return (
    <main>
      <UserNav me={me} />
      <div className="page-head">
        <div>
          <h2>Admin - users</h2>
          <p className="page-sub">Everyone with access to Team Notes, across all teams.</p>
        </div>
        <Link href="/admin/audit" className="btn">
          View audit log
        </Link>
      </div>
      <div className="stats">
        <div className="card stat">
          <p className="stat-label">Total users</p>
          <p className="stat-value">{users.length}</p>
        </div>
        <div className="card stat">
          <p className="stat-label">Admins</p>
          <p className="stat-value">{users.filter((u) => u.role === 'admin').length}</p>
        </div>
        <div className="card stat">
          <p className="stat-label">Teams</p>
          <p className="stat-value">{new Set(users.map((u) => u.team)).size}</p>
        </div>
      </div>
      <div className="card table-card">
        <div className="table-wrap">
          <table className="data-table">
            <thead>
              <tr>
                <th>User</th>
                <th>Team</th>
                <th>Role</th>
                <th>ID</th>
              </tr>
            </thead>
            <tbody>
              {users.map((u) => (
                <tr key={u.id}>
                  <td>
                    <span className="cell-user">
                      <span className="avatar" aria-hidden="true">{u.email.charAt(0)}</span>
                      {u.email}
                    </span>
                  </td>
                  <td>
                    <span className="team-pill">{u.team}</span>
                  </td>
                  <td>
                    <span className={`badge badge-${u.role === 'admin' ? 'admin' : 'member'}`}>{u.role}</span>
                  </td>
                  <td className="cell-id">#{u.id}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </main>
  );
}
