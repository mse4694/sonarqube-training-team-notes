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
