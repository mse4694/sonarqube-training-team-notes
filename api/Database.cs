using Npgsql;

namespace TeamNotes.Api;

/// <summary>Thin wrapper over an Npgsql data source. Dapper runs on the connections it hands out.</summary>
public sealed class Database : IAsyncDisposable
{
    private readonly NpgsqlDataSource _source;

    public Database(string rawConnectionString)
    {
        _source = NpgsqlDataSource.Create(AppConfig.NpgsqlConnectionString(rawConnectionString));
    }

    public ValueTask<NpgsqlConnection> OpenConnectionAsync() => _source.OpenConnectionAsync();

    public ValueTask DisposeAsync() => _source.DisposeAsync();
}

// ---- row shapes -------------------------------------------------------------
public sealed record NoteRow(int id, int team_id, int owner_id, string title, string content, string? attachment, DateTime created_at);
public sealed record NoteListRow(int id, string title, int owner_id, int team_id, DateTime created_at);
public sealed record UserRow(int id, string email, string display_name, string password_hash, string role, int team_id);
public sealed record MeRow(int id, string email, string display_name, string role, int team_id);
public sealed record AdminUserRow(int id, string email, string display_name, string role, string team);
public sealed record SearchRow(int id, string title, int team_id);
public sealed record ShareRow(int id, int note_id, string token, int created_by, DateTime created_at, DateTime? expires_at, DateTime? revoked_at);
public sealed record SharedNoteRow(string title, string content, DateTime created_at, DateTime? expires_at);
