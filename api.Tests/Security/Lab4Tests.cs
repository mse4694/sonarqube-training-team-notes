using Dapper;
using Xunit;

namespace TeamNotes.Api.Tests.Security;

public sealed class Lab4BolaTests(AppFixture app) : TestBase(app)
{
    [Fact]
    public async Task User_A_cannot_read_user_Bs_note()
    {
        var client = await App.LoginAsync("a@alpha.test", "alpha-pass-123"); // team alpha
        var (status, _) = await client.GetJsonAsync("/api/notes/3"); // note 3 belongs to team beta
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task User_A_cannot_delete_user_Bs_note()
    {
        var client = await App.LoginAsync("a@alpha.test", "alpha-pass-123");
        var res = await client.DeleteAsync("/api/notes/3");
        Assert.Equal(404, (int)res.StatusCode);

        await using var conn = await App.Database.OpenConnectionAsync();
        int count = await conn.ExecuteScalarAsync<int>("SELECT count(*) FROM notes WHERE id = 3");
        Assert.Equal(1, count); // still there
    }

    [Fact]
    public async Task A_team_admin_can_edit_a_teammates_note()
    {
        // Positive control: admin (team alpha) edits note 1 (owner A, team alpha) -> allowed on both branches.
        var client = await App.LoginAsync("admin@alpha.test", "admin-pass-123");
        var (status, _) = await client.SendJsonAsync(HttpMethod.Put, "/api/notes/1", """{"title":"edited by admin"}""");
        Assert.Equal(200, status);
    }
}

public sealed class Lab4BflaTests(AppFixture app) : TestBase(app)
{
    [Fact]
    public async Task A_member_is_forbidden_from_the_admin_api()
    {
        var client = await App.LoginAsync("a@alpha.test", "alpha-pass-123");
        var (status, _) = await client.GetJsonAsync("/api/admin/users");
        Assert.Equal(403, status);
    }

    [Fact]
    public async Task An_admin_can_use_the_admin_api()
    {
        var client = await App.LoginAsync("admin@alpha.test", "admin-pass-123");
        var (status, _) = await client.GetJsonAsync("/api/admin/users");
        Assert.Equal(200, status); // positive control
    }
}

public sealed class Lab4MassAssignmentTests(AppFixture app) : TestBase(app)
{
    [Fact]
    public async Task Patch_me_ignores_role_and_team_id()
    {
        var client = await App.LoginAsync("a@alpha.test", "alpha-pass-123");
        var (status, body) = await client.SendJsonAsync(HttpMethod.Put, "/api/me", """{"display_name":"A","role":"admin","team_id":2}""");
        Assert.Equal(200, status);
        Assert.Equal("member", body.GetProperty("user").GetProperty("role").GetString());
        Assert.Equal(1, body.GetProperty("user").GetProperty("team_id").GetInt32());
    }

    [Fact]
    public async Task Put_note_ignores_team_id_and_owner_id()
    {
        var client = await App.LoginAsync("a@alpha.test", "alpha-pass-123");
        await client.SendJsonAsync(HttpMethod.Put, "/api/notes/1", """{"title":"x","team_id":2,"owner_id":2}""");

        await using var conn = await App.Database.OpenConnectionAsync();
        var row = await conn.QueryFirstAsync("SELECT team_id, owner_id FROM notes WHERE id = 1");
        Assert.Equal(1, (int)row.team_id);
        Assert.Equal(1, (int)row.owner_id);
    }
}
