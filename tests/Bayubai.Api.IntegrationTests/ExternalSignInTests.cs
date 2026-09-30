using System.Net;
using System.Net.Http.Json;
using Bayubai.Api.IntegrationTests.Infrastructure;
using Bayubai.Identity.Endpoints;
using static Bayubai.Api.IntegrationTests.Infrastructure.SignInExtensions;

namespace Bayubai.Api.IntegrationTests;

public class ExternalSignInTests(ApiFactory factory)
{
    private const string ReturnUrl = ApiFactory.ClientAppUrl + "/";

    [Fact]
    public async Task Providers_lists_only_configured_methods()
    {
        var response = await factory.CreateHttpsClient().GetAsync("/api/identity/providers");

        var providers = await response.ReadAsAsync<ProvidersResponse>();
        providers.Providers.ShouldBe(new[] { "email", "Google", "Fake", "Telegram" });
        providers.TelegramBotName.ShouldBe("bayubai_test_bot");
    }

    [Fact]
    public async Task Start_redirects_to_the_provider_with_pkce()
    {
        var response = await factory.CreateHttpsClient().GetAsync(StartUrl("Google", ReturnUrl));

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var location = response.Headers.Location!.ToString();
        location.ShouldStartWith("https://accounts.google.com/");
        location.ShouldContain("code_challenge=");
    }

    [Fact]
    public async Task Start_with_an_unconfigured_provider_is_rejected()
    {
        var response = await factory.CreateHttpsClient().GetAsync(StartUrl("Yandex", ReturnUrl));

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "identity.provider_unavailable");
    }

    [Fact]
    public async Task Start_with_a_foreign_return_url_is_rejected()
    {
        var response = await factory.CreateHttpsClient().GetAsync(StartUrl("Google", "https://evil.example/"));

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "identity.invalid_return_url");
    }

    [Fact]
    public async Task Start_with_an_unknown_mode_is_rejected()
    {
        var response = await factory.CreateHttpsClient().GetAsync(StartUrl("Google", ReturnUrl, mode: "merge"));

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "identity.invalid_sign_in_mode");
    }

    [Fact]
    public async Task Callback_without_an_external_session_fails()
    {
        var response = await factory.CreateHttpsClient().GetAsync("/api/identity/external/callback");

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "identity.external_login_failed");
    }

    [Fact]
    public async Task Telegram_signs_in_a_new_parent()
    {
        var client = factory.CreateHttpsClient();

        var response = await client.PostAsJsonAsync("/api/identity/telegram/complete", new
        {
            auth = TelegramPayload.Create(NewTelegramId(), "Anna", factory.Clock.GetCurrentInstant()),
            language = "ru",
            timeZone = "Europe/Moscow",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var me = await client.GetMeAsync();
        me.SignInMethods.ShouldBe(new[] { "Telegram" });
        me.DisplayName.ShouldBe("Anna");
        me.Language.ShouldBe("ru");
    }

    [Fact]
    public async Task Telegram_is_linked_to_the_signed_in_user()
    {
        var client = await factory.SignedInClientAsync(NewEmail());

        var response = await client.PostAsJsonAsync("/api/identity/telegram/complete", new
        {
            auth = TelegramPayload.Create(NewTelegramId(), "Anna", factory.Clock.GetCurrentInstant()),
            mode = "link",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await client.GetMeAsync()).SignInMethods.ShouldBe(new[] { "Telegram", "email" });
    }

    [Fact]
    public async Task Telegram_account_of_another_user_cannot_be_linked()
    {
        var telegramId = NewTelegramId();
        await factory.CreateHttpsClient().PostAsJsonAsync("/api/identity/telegram/complete",
            new { auth = TelegramPayload.Create(telegramId, "Owner", factory.Clock.GetCurrentInstant()) });
        var other = await factory.SignedInClientAsync(NewEmail());

        var response = await other.PostAsJsonAsync("/api/identity/telegram/complete", new
        {
            auth = TelegramPayload.Create(telegramId, "Owner", factory.Clock.GetCurrentInstant()),
            mode = "link",
        });

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "identity.login_already_linked");
    }

    [Fact]
    public async Task Telegram_payload_signed_with_another_token_is_rejected()
    {
        var response = await factory.CreateHttpsClient().PostAsJsonAsync("/api/identity/telegram/complete",
            new { auth = TelegramPayload.Create(NewTelegramId(), "Anna", factory.Clock.GetCurrentInstant(), botToken: "999:OTHER") });

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "identity.external_login_failed");
    }

    private static string StartUrl(string provider, string returnUrl, string mode = "signin") =>
        $"/api/identity/external/{provider}/start?returnUrl={Uri.EscapeDataString(returnUrl)}&mode={mode}&language=en&timeZone=UTC";

    private static string NewTelegramId() => Random.Shared.NextInt64(1_000_000, long.MaxValue).ToString(System.Globalization.CultureInfo.InvariantCulture);
}
