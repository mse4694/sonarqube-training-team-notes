using Dapper;

namespace TeamNotes.Api;

/// <summary>Creates the schema if missing and seeds training data. Idempotent (safe on every boot).</summary>
public static class DbInitializer
{
    public const string SchemaSql = """
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
        CREATE TABLE IF NOT EXISTS note_shares (
          id         SERIAL PRIMARY KEY,
          note_id    INT  NOT NULL REFERENCES notes(id) ON DELETE CASCADE,
          token      TEXT UNIQUE NOT NULL,
          created_by INT  NOT NULL REFERENCES users(id) ON DELETE CASCADE,
          created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
          expires_at TIMESTAMPTZ,
          revoked_at TIMESTAMPTZ
        );
        """;

    public static async Task EnsureCreatedAsync(Database db, AuthService auth)
    {
        await using var conn = await db.OpenConnectionAsync();
        await conn.ExecuteAsync(SchemaSql);

        int userCount = await conn.ExecuteScalarAsync<int>("SELECT count(*) FROM users");
        if (userCount > 0)
        {
            return;
        }

        await conn.ExecuteAsync(
            "INSERT INTO teams (id, slug, name) VALUES (1,'alpha','Team Alpha'), (2,'beta','Team Beta')");

        // Passwords: alpha-pass-123 / beta-pass-123 / admin-pass-123. Hashed with the branch's
        // HashPassword (md5 on main, PBKDF2 on solution) so the seed always matches the verifier.
        await conn.ExecuteAsync(
            "INSERT INTO users (id, email, display_name, password_hash, role, team_id) VALUES " +
            "(1,@aE,'User A',@aH,'member',1),(2,@bE,'User B',@bH,'member',2),(3,@dE,'Admin',@dH,'admin',1)",
            new
            {
                aE = "a@alpha.test", aH = auth.HashPassword("alpha-pass-123"),
                bE = "b@beta.test", bH = auth.HashPassword("beta-pass-123"),
                dE = "admin@alpha.test", dH = auth.HashPassword("admin-pass-123"),
            });

        await conn.ExecuteAsync(
            "INSERT INTO notes (id, team_id, owner_id, title, content, attachment) VALUES " +
            "(1,1,1,'Sprint 12 plan','Ship search, fix login bug, <b>demo on Friday</b>',NULL)," +
            "(2,1,1,'Release checklist','See attached checklist before tagging v1.4','release-checklist.txt')," +
            "(3,2,2,'Beta salary review','CONFIDENTIAL: Beta team salary bands for 2027',NULL)," +
            "(4,2,2,'Beta vendor contracts','Renewal dates and negotiated prices (Beta only)',NULL)," +
            "(5,1,3,'Alpha onboarding','Welcome! Read the handbook first.','welcome.txt')");

        await conn.ExecuteAsync(
            "SELECT setval('teams_id_seq',2); SELECT setval('users_id_seq',3); SELECT setval('notes_id_seq',5);");
    }
}
