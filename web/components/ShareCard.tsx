'use client';

import { useState } from 'react';
import { expiryLabel, shareStatus, type Share } from '@/lib/share';

export function ShareCard({ noteId, initialShares }: Readonly<{ noteId: number; initialShares: Share[] }>) {
  const [shares, setShares] = useState<Share[]>(initialShares);
  const [message, setMessage] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function refresh() {
    const res = await fetch(`/api/notes/${noteId}/shares`, { cache: 'no-store' });
    if (res.ok) setShares(((await res.json()).shares ?? []) as Share[]);
  }

  async function create() {
    setBusy(true);
    setMessage(null);
    const res = await fetch(`/api/notes/${noteId}/share`, {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({ expires_in_hours: 24 }),
    });
    setBusy(false);
    if (!res.ok) {
      setMessage('Could not create a link.');
      return;
    }
    const { share } = (await res.json()) as { share: Share };
    setMessage(`Link created: ${window.location.origin}${share.url}`);
    await refresh();
  }

  async function revokeAll() {
    setBusy(true);
    setMessage(null);
    const res = await fetch(`/api/notes/${noteId}/share`, { method: 'DELETE' });
    setBusy(false);
    setMessage(res.ok ? 'All public links were revoked.' : 'Could not revoke the links.');
    await refresh();
  }

  async function copy(url: string) {
    try {
      await navigator.clipboard.writeText(`${window.location.origin}${url}`);
      setMessage('Link copied to clipboard.');
    } catch {
      setMessage(`${window.location.origin}${url}`);
    }
  }

  const active = shares.filter((s) => shareStatus(s) === 'active');

  return (
    <section className="card share-card">
      <div className="share-head">
        <div>
          <h3>Share</h3>
          <p className="card-sub">Anyone with a public link can read this note without signing in.</p>
        </div>
        <div className="share-actions">
          <button type="button" className="btn-primary" onClick={create} disabled={busy}>
            Create public link
          </button>
          {active.length > 0 && (
            <button type="button" className="btn-ghost" onClick={revokeAll} disabled={busy}>
              Revoke all
            </button>
          )}
        </div>
      </div>
      {message && <p className="alert alert-info">{message}</p>}
      {shares.length === 0 ? (
        <p className="empty">No public links yet.</p>
      ) : (
        <ul className="share-list">
          {shares.map((s) => {
            const status = shareStatus(s);
            return (
              <li key={s.id} className="share-item">
                <code className="token" title={s.token}>
                  {s.url}
                </code>
                <span className={`badge badge-${status}`}>{status}</span>
                <span className="share-meta">{expiryLabel(s.expires_at)}</span>
                {status === 'active' && (
                  <button type="button" className="btn-ghost btn-sm" onClick={() => copy(s.url)}>
                    Copy
                  </button>
                )}
              </li>
            );
          })}
        </ul>
      )}
    </section>
  );
}
