import Link from 'next/link';
import { redirect } from 'next/navigation';
import { apiGet } from '@/lib/api';
import { formatUtc } from '@/lib/audit';
import { UserNav, type Me } from '@/components/UserNav';

export const dynamic = 'force-dynamic';

type AuditEvent = {
  id: number;
  at: string;
  actor_email: string | null;
  action: string;
  target: string | null;
  ip: string | null;
  detail: string | null;
};

export default async function AuditPage() {
  const res = await apiGet('/api/admin/audit');
  if (res.status === 401) redirect('/login?next=/admin/audit');
  if (res.status === 403) redirect('/notes');
  const events = ((await res.json()).events ?? []) as AuditEvent[];
  const me = (await (await apiGet('/api/me')).json()).user as Me;

  return (
    <main>
      <UserNav me={me} />
      <Link href="/admin" className="back-link">&larr; Admin</Link>
      <div className="page-head">
        <div>
          <h2>Audit log</h2>
          <p className="page-sub">Security-relevant events: sign-ins, API keys, share links. Latest 200.</p>
        </div>
      </div>
      <div className="card table-card">
        {events.length === 0 ? (
          <p className="empty table-empty">No audit events recorded yet.</p>
        ) : (
          <div className="table-wrap">
            <table className="data-table">
              <thead>
                <tr>
                  <th>Time (UTC)</th>
                  <th>Action</th>
                  <th>Actor</th>
                  <th>Target</th>
                  <th>IP</th>
                  <th>Detail</th>
                </tr>
              </thead>
              <tbody>
                {events.map((e) => (
                  <tr key={e.id}>
                    <td className="cell-id">{formatUtc(e.at)}</td>
                    <td>
                      <code className="inline-code">{e.action}</code>
                    </td>
                    <td>{e.actor_email ?? '-'}</td>
                    <td>{e.target ?? '-'}</td>
                    <td className="cell-id">{e.ip ?? '-'}</td>
                    <td>{e.detail ?? ''}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </main>
  );
}
