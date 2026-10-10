import Link from 'next/link';

export type Me = { email: string; role: string; display_name?: string };

// Signed-in user chip + navigation, rendered inside the top bar (see .user-nav in globals.css).
export function UserNav({ me }: Readonly<{ me: Me }>) {
  return (
    <nav className="user-nav">
      <span className="user-chip">
        <span className="avatar" aria-hidden="true">{me.email.charAt(0)}</span>
        <span>
          Signed in as <strong>{me.email}</strong>
        </span>
      </span>
      <Link href="/notes" className="nav-link">
        Notes
      </Link>
      <Link href="/settings" className="nav-link">
        Settings
      </Link>
      {me.role === 'admin' && (
        <Link href="/admin" className="nav-link">
          Admin
        </Link>
      )}
      <form method="post" action="/api/auth/logout">
        <button type="submit" className="btn-ghost">Sign out</button>
      </form>
    </nav>
  );
}
