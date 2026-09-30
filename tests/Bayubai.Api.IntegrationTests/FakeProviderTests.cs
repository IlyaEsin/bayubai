using System.Net;
using Bayubai.Api.IntegrationTests.Infrastructure;
using static Bayubai.Api.IntegrationTests.Infrastructure.SignInExtensions;

namespace Bayubai.Api.IntegrationTests;

public class FakeProviderTests(ApiFactory factory)
{
    private const string ReturnUrl = ApiFactory.ClientAppUrl + "/auth/external";

    [Fact]
    public async Task Round_trip_signs_in_a_new_parent_and_returns_to_the_app()
    {
        var client = factory.CreateHttpsClient();
        var subject = $"fake-{Guid.NewGuid():N}";

        var location = await RoundTripAsync(client, subject, "signin");

        location.ShouldBe(ReturnUrl);
        var me = await client.GetMeAsync();
        me.SignInMethods.ShouldBe(new[] { "Fake" });
        me.DisplayName.ShouldBe(subject);
    }

    [Fact]
    public async Task Signed_in_user_links_the_fake_provider_and_signs_in_with_it()
    {
        var client = await factory.SignedInClientAsync(NewEmail());
        var subject = $"fake-{Guid.NewGuid():N}";

        await RoundTripAsync(client, subject, "link");

        (await client.GetMeAsync()).SignInMethods.ShouldBe(new[] { "Fake", "email" });
        var viaFake = factory.CreateHttpsClient();
        await RoundTripAsync(viaFake, subject, "signin");
        (await viaFake.GetMeAsync()).Id.ShouldBe((await client.GetMeAsync()).Id);
    }

    [Fact]
    public async Task Linking_a_subject_owned_by_another_account_redirects_with_an_error_code()
    {
        var subject = $"fake-{Guid.NewGuid():N}";
        await RoundTripAsync(factory.CreateHttpsClient(), subject, "signin");
        var other = await factory.SignedInClientAsync(NewEmail());

        var location = await RoundTripAsync(other, subject, "link");

        location.ShouldBe(ReturnUrl + "?error=identity.login_already_linked");
    }

    [Fact]
    public async Task Callback_with_a_forged_state_redirects_home()
    {
        var response = await factory.CreateHttpsClient().GetAsync("/api/identity/signin-fake?state=forged&subject=x");

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().ShouldBe("/");
    }

    // Follows start, the provider callback and the API callback by hand and returns the final redirect into the app.
    private static async Task<string> RoundTripAsync(HttpClient client, string subject, string mode)
    {
        var start = new HttpRequestMessage(HttpMethod.Get,
            $"/api/identity/external/Fake/start?returnUrl={Uri.EscapeDataString(ReturnUrl)}&mode={mode}&language=en&timeZone=UTC");
        start.Headers.Add("Cookie", $"bb_fake_subject={subject}");
        var challenge = await client.SendAsync(start);
        challenge.StatusCode.ShouldBe(HttpStatusCode.Redirect);

        var providerCallback = await client.GetAsync(challenge.Headers.Location);
        providerCallback.StatusCode.ShouldBe(HttpStatusCode.Redirect);

        var apiCallback = await client.GetAsync(providerCallback.Headers.Location);
        apiCallback.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        return apiCallback.Headers.Location!.ToString();
    }
}
