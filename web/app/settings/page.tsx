import { redirect } from 'next/navigation';
import { apiGet } from '@/lib/api';
import { ApiKeysCard, type ApiKey } from '@/components/ApiKeysCard';
import { UserNav, type Me } from '@/components/UserNav';

export const dynamic = 'force-dynamic';

export default async function SettingsPage() {
  const meRes = await apiGet('/api/me');
  if (meRes.status === 401) redirect('/login?next=/settings');
  const me = (await meRes.json()).user as Me & { display_name: string; team_id: number };
  const keysRes = await apiGet('/api/me/api-keys');
  const keys = (keysRes.ok ? ((await keysRes.json()).keys ?? []) : []) as ApiKey[];

  return (
    <main>
      <UserNav me={me} />
      <div className="page-head">
        <div>
          <h2>Settings</h2>
          <p className="page-sub">Your profile and the API keys your scripts use.</p>
        </div>
      </div>
      <div className="settings-layout">
        <section className="card profile-card">
          <h3>Profile</h3>
          <dl className="kv">
            <dt>Name</dt>
            <dd>{me.display_name}</dd>
            <dt>Email</dt>
            <dd>{me.email}</dd>
            <dt>Role</dt>
            <dd>
              <span className={`badge badge-${me.role === 'admin' ? 'admin' : 'member'}`}>{me.role}</span>
            </dd>
            <dt>Team</dt>
            <dd className="cell-id">#{me.team_id}</dd>
          </dl>
        </section>
        <ApiKeysCard initialKeys={keys} />
      </div>
    </main>
  );
}
