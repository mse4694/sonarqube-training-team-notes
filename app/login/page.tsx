import { redirect } from 'next/navigation';
import { getSession } from '@/lib/auth';

type Props = { searchParams: Promise<{ next?: string; error?: string }> };

export default async function LoginPage({ searchParams }: Props) {
  const { next = '/notes', error } = await searchParams;
  // Already logged in? Go straight to where the user wanted to go.
  if (await getSession()) redirect(next);

  return (
    <form method="post" action="/api/auth/login" style={{ display: 'grid', gap: '0.75rem', maxWidth: 320 }}>
      <h2>Sign in</h2>
      {error && <p style={{ color: 'crimson' }}>Wrong email or password</p>}
      <input type="hidden" name="next" value={next} />
      <input name="email" type="email" placeholder="email" required />
      <input name="password" type="password" placeholder="password" required />
      <label>
        <input type="checkbox" name="remember" /> Remember me for 30 days
      </label>
      <button type="submit">Sign in</button>
    </form>
  );
}
