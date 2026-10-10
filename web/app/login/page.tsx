type Props = { searchParams: Promise<{ next?: string; error?: string }> };

export default async function LoginPage({ searchParams }: Props) {
  const { next = '/notes', error } = await searchParams;
  return (
    <form method="post" action="/api/auth/login" className="card auth-card">
      <h2>Sign in</h2>
      <p className="auth-intro">Welcome back. Sign in with your team account.</p>
      {error && <p className="alert alert-error">Wrong email or password</p>}
      <input type="hidden" name="next" value={next} />
      <label className="field">
        <span>Email</span>
        <input name="email" type="email" placeholder="email" required />
      </label>
      <label className="field">
        <span>Password</span>
        <input name="password" type="password" placeholder="password" required />
      </label>
      <label className="checkbox">
        <input type="checkbox" name="remember" /> Remember me for 30 days
      </label>
      <button type="submit" className="btn-primary btn-block">Sign in</button>
    </form>
  );
}
