using Xunit;

namespace TeamNotes.Api.Tests.Security;

public sealed class Lab2SecretsTests(AppFixture app) : TestBase(app)
{
    [Fact]
    public void No_hard_coded_database_password_in_the_fallback_connection_string()
    {
        Assert.DoesNotContain("Tn7vQ2mZ9rK4xW", AppConfig.FallbackDatabaseUrl);
    }

    [Fact]
    public void No_hard_coded_aws_secret_access_key()
    {
        Assert.DoesNotContain("wJalrXUtnFEMI", AppConfig.AwsSecretAccessKey);
    }

    [Fact]
    public void Jwt_signing_key_is_not_the_hard_coded_training_value()
    {
        Assert.NotEqual("team-notes-jwt-secret-2026-do-not-share-0123456789", AppConfig.JwtSecret);
    }
}

public sealed class Lab6ScaTests(AppFixture app) : TestBase(app)
{
    [Fact]
    public void Newtonsoft_Json_is_at_least_13_0_0()
    {
        var version = typeof(Newtonsoft.Json.JsonConvert).Assembly.GetName().Version!;
        Assert.True(
            version.Major >= 13,
            $"Newtonsoft.Json {version} has advisory GHSA-5crp-9r3c-p9vr; upgrade to >= 13.0.0");
    }
}
