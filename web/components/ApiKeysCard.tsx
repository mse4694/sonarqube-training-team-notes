'use client';

import { useState } from 'react';
import { maskKey } from '@/lib/api-keys';

export type ApiKey = {
  id: number;
  name: string;
  prefix: string;
  key?: string;
  created_at: string;
  last_used_at: string | null;
  revoked_at: string | null;
};

export function ApiKeysCard({ initialKeys }: Readonly<{ initialKeys: ApiKey[] }>) {
  const [keys, setKeys] = useState<ApiKey[]>(initialKeys);
  const [name, setName] = useState('');
  const [created, setCreated] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function refresh() {
    const res = await fetch('/api/me/api-keys', { cache: 'no-store' });
    if (res.ok) setKeys(((await res.json()).keys ?? []) as ApiKey[]);
  }

  async function create(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    const res = await fetch('/api/me/api-keys', {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({ name }),
    });
    if (!res.ok) {
      setError('Could not create the key.');
      return;
    }
    setCreated(((await res.json()) as { key: string }).key);
    setName('');
    await refresh();
  }

  async function revoke(id: number) {
    setError(null);
    const res = await fetch(`/api/me/api-keys/${id}`, { method: 'DELETE' });
    if (!res.ok) setError('Could not revoke the key.');
    await refresh();
  }

  return (
    <section className="card form-card api-keys-card">
      <h3>API keys</h3>
      <p className="card-sub">
        Send a key in the <code className="inline-code">X-Api-Key</code> header to call the API from scripts.
      </p>
      <form onSubmit={create} className="inline-form">
        <input value={name} onChange={(e) => setName(e.target.value)} placeholder="Key name, e.g. ci-export" maxLength={60} required />
        <button type="submit" className="btn-primary">
          Create key
        </button>
      </form>
      {created && (
        <div className="alert alert-success">
          <p className="alert-title">Copy your new key now. You will not be able to see it again.</p>
          <code className="token token-full">{created}</code>
        </div>
      )}
      {error && <p className="alert alert-error">{error}</p>}
      {keys.length === 0 ? (
        <p className="empty">No API keys yet.</p>
      ) : (
        <ul className="share-list">
          {keys.map((k) => (
            <li key={k.id} className="share-item">
              <span className="key-name">{k.name}</span>
              <code className="token">{k.key ?? maskKey(k.prefix)}</code>
              <span className={`badge badge-${k.revoked_at ? 'revoked' : 'active'}`}>{k.revoked_at ? 'revoked' : 'active'}</span>
              <span className="share-meta">{k.last_used_at ? 'Used recently' : 'Never used'}</span>
              {!k.revoked_at && (
                <button type="button" className="btn-ghost btn-sm" onClick={() => revoke(k.id)}>
                  Revoke
                </button>
              )}
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
