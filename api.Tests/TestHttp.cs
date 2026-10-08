using System.Text;
using System.Text.Json;

namespace TeamNotes.Api.Tests;

public static class TestHttp
{
    public static async Task<(int status, JsonElement body)> GetJsonAsync(this HttpClient client, string path)
    {
        var res = await client.GetAsync(path);
        return (await Read(res));
    }

    public static async Task<(int status, JsonElement body)> SendJsonAsync(this HttpClient client, HttpMethod method, string path, string json)
    {
        var req = new HttpRequestMessage(method, path) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        var res = await client.SendAsync(req);
        return (await Read(res));
    }

    private static async Task<(int, JsonElement)> Read(HttpResponseMessage res)
    {
        string text = await res.Content.ReadAsStringAsync();
        JsonElement body = default;
        if (!string.IsNullOrWhiteSpace(text))
        {
            try { body = JsonDocument.Parse(text).RootElement.Clone(); }
            catch (JsonException) { /* non-JSON (e.g. a file download) */ }
        }
        return ((int)res.StatusCode, body);
    }

    public static string Text(this JsonElement body) => body.ValueKind == JsonValueKind.Undefined ? "" : body.GetRawText();
}
