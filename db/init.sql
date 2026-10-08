-- Team Notes schema + seed data (runs once when the db container is created).
CREATE TABLE teams (
  id   SERIAL PRIMARY KEY,
  slug TEXT UNIQUE NOT NULL,
  name TEXT NOT NULL
);

CREATE TABLE users (
  id            SERIAL PRIMARY KEY,
  email         TEXT UNIQUE NOT NULL,
  display_name  TEXT NOT NULL,
  password_hash TEXT NOT NULL,
  role          TEXT NOT NULL DEFAULT 'member' CHECK (role IN ('member', 'admin')),
  team_id       INT  NOT NULL REFERENCES teams(id)
);

CREATE TABLE notes (
  id         SERIAL PRIMARY KEY,
  team_id    INT  NOT NULL REFERENCES teams(id),
  owner_id   INT  NOT NULL REFERENCES users(id),
  title      TEXT NOT NULL,
  content    TEXT NOT NULL DEFAULT '',
  attachment TEXT,
  created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

INSERT INTO teams (id, slug, name) VALUES
  (1, 'alpha', 'Team Alpha'),
  (2, 'beta',  'Team Beta');

-- Training passwords: alpha-pass-123 / beta-pass-123 / admin-pass-123
INSERT INTO users (id, email, display_name, password_hash, role, team_id) VALUES
  (1, 'a@alpha.test',     'User A', md5('alpha-pass-123'), 'member', 1),
  (2, 'b@beta.test',      'User B', md5('beta-pass-123'),  'member', 2),
  (3, 'admin@alpha.test', 'Admin',  md5('admin-pass-123'), 'admin',  1);

INSERT INTO notes (id, team_id, owner_id, title, content, attachment) VALUES
  (1, 1, 1, 'Sprint 12 plan',        'Ship search, fix login bug, <b>demo on Friday</b>', NULL),
  (2, 1, 1, 'Release checklist',     'See attached checklist before tagging v1.4', 'release-checklist.txt'),
  (3, 2, 2, 'Beta salary review',    'CONFIDENTIAL: Beta team salary bands for 2027', NULL),
  (4, 2, 2, 'Beta vendor contracts', 'Renewal dates and negotiated prices (Beta only)', NULL),
  (5, 1, 3, 'Alpha onboarding',      'Welcome! Read the handbook first.', 'welcome.txt');

SELECT setval('teams_id_seq', 2);
SELECT setval('users_id_seq', 3);
SELECT setval('notes_id_seq', 5);
