using System.Text.Json;
using System.Text.RegularExpressions;
using Dapper;
using Npgsql;

namespace TeamNotes.Api.Endpoints;

public static partial class Mutations
{
    [GeneratedRegex("^[a-z_]+$")]
    private static partial Regex Identifier();

    /// <summary>
    /// Generic "UPDATE &lt;table&gt; SET &lt;fields&gt; WHERE id = @id" helper. Identifiers are
    /// validated/quoted and values are bound, so this is NOT a SQL-injection sink. It copies every
    /// field it is given, which is exactly the mass-assignment foot-gun the callers demonstrate.
    /// </summary>
    public static async Task<int?> UpdateRowAsync(NpgsqlConnection conn, string table, int id, IReadOnlyDictionary<string, object?> fields)
    {
        var sets = new List<string>();
        var parameters = new DynamicParameters();
        parameters.Add("id", id);

        int i = 0;
        foreach (var (key, value) in fields)
        {
            if (!Identifier().IsMatch(key))
            {
                continue;
            }
            string pn = "p" + i++;
            sets.Add($"\"{key}\" = @{pn}");
            parameters.Add(pn, value);
        }

        if (sets.Count == 0)
        {
            return null;
        }

        string sql = $"UPDATE {table} SET {string.Join(", ", sets)} WHERE id = @id RETURNING id";
        var rows = (await conn.QueryAsync<int>(sql, parameters)).ToList();
        return rows.Count > 0 ? rows[0] : null;
    }

    public static Dictionary<string, object?> FromJson(Dictionary<string, JsonElement>? body)
    {
        var result = new Dictionary<string, object?>();
        if (body is null)
        {
            return result;
        }
        foreach (var (key, value) in body)
        {
            result[key] = value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.TryGetInt64(out long l) ? l : value.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null,
            };
        }
        return result;
    }
}
