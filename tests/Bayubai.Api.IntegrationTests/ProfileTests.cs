using System.Net;
using System.Net.Http.Json;
using Bayubai.Api.IntegrationTests.Infrastructure;
using Bayubai.Identity.Endpoints;
using static Bayubai.Api.IntegrationTests.Infrastructure.SignInExtensions;

namespace Bayubai.Api.IntegrationTests;

public class ProfileTests(ApiFactory factory)
{
    [Fact]
    public async Task Profile_update_is_saved()
    {
        var client = await factory.SignedInClientAsync(NewEmail());

        var response = await client.PutAsJsonAsync("/api/identity/me", new { displayName = "  Anna Petrova ", language = "ru", timeZone = "Asia/Novosibirsk" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.ReadAsAsync<MeResponse>()).DisplayName.ShouldBe("Anna Petrova");
        var me = await client.GetMeAsync();
        me.DisplayName.ShouldBe("Anna Petrova");
        me.Language.ShouldBe("ru");
        me.TimeZone.ShouldBe("Asia/Novosibirsk");
    }

    [Theory]
    [InlineData("", "ru", "UTC", "displayName")]
    [InlineData("   ", "ru", "UTC", "displayName")]
    [InlineData("Anna", "de", "UTC", "language")]
    [InlineData("Anna", "ru", "Moscow", "timeZone")]
    public async Task Invalid_profile_is_rejected(string displayName, string language, string timeZone, string field)
    {
        var client = await factory.SignedInClientAsync(NewEmail());

        var response = await client.PutAsJsonAsync("/api/identity/me", new { displayName, language, timeZone });

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        (await response.Content.ReadAsStringAsync()).ShouldContain($"\"{field}\"");
    }

    [Fact]
    public async Task Profile_update_requires_a_session()
    {
        var response = await factory.CreateHttpsClient().PutAsJsonAsync("/api/identity/me", new { displayName = "Anna", language = "ru", timeZone = "UTC" });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
