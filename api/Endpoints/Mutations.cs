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

    /// <summary>Copy only allow-listed string fields from an untrusted body (blocks mass assignment).</summary>
    public static Dictionary<string, object?> PickStrings(Dictionary<string, JsonElement>? body, params string[] allowed)
    {
        var picked = new Dictionary<string, object?>();
        if (body is null)
        {
            return picked;
        }
        foreach (var key in allowed)
        {
            if (body.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.String)
            {
                picked[key] = value.GetString();
            }
        }
        return picked;
    }
}
