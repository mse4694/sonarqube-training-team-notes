-- Team Notes schema (runs once when the db container is created).
-- The API also runs CREATE TABLE IF NOT EXISTS on boot, then seeds teams/users/notes in C#
-- so the seeded password hashes match the branch's HashPassword (md5 on main, PBKDF2 on solution).
CREATE TABLE IF NOT EXISTS teams (
  id   SERIAL PRIMARY KEY,
  slug TEXT UNIQUE NOT NULL,
  name TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS users (
  id            SERIAL PRIMARY KEY,
  email         TEXT UNIQUE NOT NULL,
  display_name  TEXT NOT NULL,
  password_hash TEXT NOT NULL,
  role          TEXT NOT NULL DEFAULT 'member' CHECK (role IN ('member', 'admin')),
  team_id       INT  NOT NULL REFERENCES teams(id)
);

CREATE TABLE IF NOT EXISTS notes (
  id         SERIAL PRIMARY KEY,
  team_id    INT  NOT NULL REFERENCES teams(id),
  owner_id   INT  NOT NULL REFERENCES users(id),
  title      TEXT NOT NULL,
  content    TEXT NOT NULL DEFAULT '',
  attachment TEXT,
  created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- Public share links (POST /api/notes/{id}/share, GET /api/share/{token}).
CREATE TABLE IF NOT EXISTS note_shares (
  id         SERIAL PRIMARY KEY,
  note_id    INT  NOT NULL REFERENCES notes(id) ON DELETE CASCADE,
  token      TEXT UNIQUE NOT NULL,
  created_by INT  NOT NULL REFERENCES users(id) ON DELETE CASCADE,
  created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
  expires_at TIMESTAMPTZ,
  revoked_at TIMESTAMPTZ
);

-- Personal API keys (Settings page, header X-Api-Key). `secret` holds the key material
-- (plaintext on main, SHA-256 hex on solution).
CREATE TABLE IF NOT EXISTS api_keys (
  id           SERIAL PRIMARY KEY,
  user_id      INT  NOT NULL REFERENCES users(id) ON DELETE CASCADE,
  name         TEXT NOT NULL,
  prefix       TEXT NOT NULL,
  secret       TEXT NOT NULL,
  created_at   TIMESTAMPTZ NOT NULL DEFAULT now(),
  last_used_at TIMESTAMPTZ,
  revoked_at   TIMESTAMPTZ
);
CREATE INDEX IF NOT EXISTS api_keys_prefix_idx ON api_keys (prefix);

-- Security audit trail (admin page /admin/audit). No FK on actor_id: events outlive users.
CREATE TABLE IF NOT EXISTS audit_log (
  id          SERIAL PRIMARY KEY,
  at          TIMESTAMPTZ NOT NULL DEFAULT now(),
  actor_id    INT,
  actor_email TEXT,
  action      TEXT NOT NULL,
  target      TEXT,
  ip          TEXT,
  detail      TEXT
);
