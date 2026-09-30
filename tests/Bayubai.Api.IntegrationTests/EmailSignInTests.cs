using System.Net;
using System.Net.Http.Json;
using Bayubai.Api.IntegrationTests.Infrastructure;
using Bayubai.Identity;
using Bayubai.Identity.Accounts;
using NodaTime;
using static Bayubai.Api.IntegrationTests.Infrastructure.SignInExtensions;

namespace Bayubai.Api.IntegrationTests;

public class EmailSignInTests(ApiFactory factory)
{
    [Fact]
    public async Task New_email_signs_in_as_parent_with_browser_defaults()
    {
        var email = NewEmail();
        var client = factory.CreateHttpsClient();

        await client.SignInWithEmailAsync(factory, email, language: "ru", timeZone: "Asia/Yekaterinburg");

        var me = await client.GetMeAsync();
        me.Roles.ShouldBe(new[] { IdentityRoles.Parent });
        me.SignInMethods.ShouldBe(new[] { EmailLogin.Provider });
        me.Language.ShouldBe("ru");
        me.TimeZone.ShouldBe("Asia/Yekaterinburg");
        me.DisplayName.ShouldBe(email[..email.IndexOf('@')]);
        factory.Emails.SentTo(email)[0].Subject.ShouldBe("Вход в Баюбай");
    }

    [Fact]
    public async Task Email_case_and_spaces_reach_one_account()
    {
        var local = $"anna-{Guid.NewGuid():N}";
        var first = factory.CreateHttpsClient();
        var second = factory.CreateHttpsClient();

        await first.SignInWithEmailAsync(factory, $"  {local.ToUpperInvariant()}@Example.TEST ");
        await second.SignInWithEmailAsync(factory, $"{local}@example.test");

        (await second.GetMeAsync()).Id.ShouldBe((await first.GetMeAsync()).Id);
    }

    [Fact]
    public async Task Magic_link_works_once()
    {
        var email = NewEmail();
        var client = factory.CreateHttpsClient();
        await client.SignInWithEmailAsync(factory, email);

        var reuse = await factory.CreateHttpsClient().PostAsJsonAsync("/api/identity/email/complete", new { token = factory.Emails.LatestTokenFor(email) });

        await reuse.ShouldBeProblemAsync(HttpStatusCode.Conflict, "identity.magic_link_used");
    }

    [Fact]
    public async Task Magic_link_expires_after_15_minutes()
    {
        var email = NewEmail();
        var client = factory.CreateHttpsClient();
        await StartAsync(client, email);
        factory.Clock.Advance(Duration.FromMinutes(15));

        var complete = await client.PostAsJsonAsync("/api/identity/email/complete", new { token = factory.Emails.LatestTokenFor(email) });

        await complete.ShouldBeProblemAsync(HttpStatusCode.Gone, "identity.magic_link_expired");
    }

    [Fact]
    public async Task Unknown_token_is_rejected()
    {
        var complete = await factory.CreateHttpsClient().PostAsJsonAsync("/api/identity/email/complete", new { token = "unknown" });

        await complete.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "identity.magic_link_invalid");
    }

    [Fact]
    public async Task Callback_url_outside_frontend_origins_is_rejected_and_nothing_is_sent()
    {
        var email = NewEmail();

        var start = await factory.CreateHttpsClient().PostAsJsonAsync("/api/identity/email/start",
            new { email, callbackUrl = "https://app.example.test.evil.com/auth/email", language = "en", timeZone = "UTC" });

        await start.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "identity.invalid_return_url");
        factory.Emails.SentTo(email).ShouldBeEmpty();
    }

    [Fact]
    public async Task Start_sends_at_most_three_links_per_email_in_ten_minutes()
    {
        var email = NewEmail();
        var client = factory.CreateHttpsClient();

        for (var i = 0; i < 4; i++)
        {
            (await StartAsync(client, email)).StatusCode.ShouldBe(HttpStatusCode.Accepted);
        }

        factory.Emails.SentTo(email).Count.ShouldBe(3);
        factory.Clock.Advance(Duration.FromMinutes(11));
        await StartAsync(client, email);
        factory.Emails.SentTo(email).Count.ShouldBe(4);
    }

    [Fact]
    public async Task Concurrent_starts_for_the_same_email_still_cap_at_three()
    {
        var email = NewEmail();
        var client = factory.CreateHttpsClient();

        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => StartAsync(client, email)));

        responses.ShouldAllBe(response => response.StatusCode == HttpStatusCode.Accepted);
        factory.Emails.SentTo(email).Count.ShouldBe(3);
    }

    [Fact]
    public async Task Configured_admin_email_gets_the_admin_role()
    {
        var admin = await factory.GetAdminClientAsync();

        (await admin.GetMeAsync()).Roles.ShouldContain(IdentityRoles.Admin);
    }

    [Fact]
    public async Task Invalid_request_returns_coded_validation_problem()
    {
        var start = await factory.CreateHttpsClient().PostAsJsonAsync("/api/identity/email/start",
            new { email = NewEmail(), callbackUrl = EmailCallbackUrl, language = "de", timeZone = "Moscow" });

        await start.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        var body = await start.Content.ReadAsStringAsync();
        body.ShouldContain("\"language\"");
        body.ShouldContain("\"timeZone\"");
    }

    [Fact]
    public async Task Mailbox_with_a_display_name_is_rejected_and_nothing_is_sent()
    {
        var local = $"anna-{Guid.NewGuid():N}";
        var email = $"{local}@example.test";

        var start = await factory.CreateHttpsClient().PostAsJsonAsync("/api/identity/email/start",
            new { email = $"Anna <{email}>", callbackUrl = EmailCallbackUrl, language = "en", timeZone = "UTC" });

        await start.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "validation_failed");
        (await start.Content.ReadAsStringAsync()).ShouldContain("\"email\"");
        factory.Emails.SentTo(email).ShouldBeEmpty();
    }

    [Fact]
    public async Task Link_mode_requires_a_signed_in_user()
    {
        var start = await factory.CreateHttpsClient().PostAsJsonAsync("/api/identity/email/start",
            new { email = NewEmail(), callbackUrl = EmailCallbackUrl, language = "en", timeZone = "UTC", mode = "link" });

        await start.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "identity.not_signed_in");
    }

    [Fact]
    public async Task Signed_in_user_links_a_second_email()
    {
        var client = await factory.SignedInClientAsync(NewEmail());
        var secondEmail = NewEmail();
        await StartAsync(client, secondEmail, mode: "link");

        var complete = await client.PostAsJsonAsync("/api/identity/email/complete", new { token = factory.Emails.LatestTokenFor(secondEmail) });

        complete.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var viaSecond = await factory.SignedInClientAsync(secondEmail);
        (await viaSecond.GetMeAsync()).Id.ShouldBe((await client.GetMeAsync()).Id);
    }

    [Fact]
    public async Task Link_token_cannot_be_completed_from_another_session()
    {
        var client = await factory.SignedInClientAsync(NewEmail());
        var secondEmail = NewEmail();
        await StartAsync(client, secondEmail, mode: "link");

        var complete = await factory.CreateHttpsClient().PostAsJsonAsync("/api/identity/email/complete", new { token = factory.Emails.LatestTokenFor(secondEmail) });

        await complete.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "identity.link_session_mismatch");
    }

    [Fact]
    public async Task Me_requires_a_session_and_sign_out_ends_it()
    {
        (await factory.CreateHttpsClient().GetAsync("/api/identity/me")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var client = await factory.SignedInClientAsync(NewEmail());

        (await client.PostAsync("/api/identity/signout", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await client.GetAsync("/api/identity/me")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Magic_link_opened_in_another_browser_is_rejected_and_stays_usable()
    {
        var email = NewEmail();
        var requester = factory.CreateHttpsClient();
        await StartAsync(requester, email);
        var token = factory.Emails.LatestTokenFor(email);

        var elsewhere = await factory.CreateHttpsClient().PostAsJsonAsync("/api/identity/email/complete", new { token });

        await elsewhere.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "identity.magic_link_other_browser");
        (await requester.PostAsJsonAsync("/api/identity/email/complete", new { token })).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task A_second_request_keeps_the_first_link_valid_in_the_same_browser()
    {
        var email = NewEmail();
        var client = factory.CreateHttpsClient();
        await StartAsync(client, email);
        var first = factory.Emails.LatestTokenFor(email);
        await StartAsync(client, email);

        var complete = await client.PostAsJsonAsync("/api/identity/email/complete", new { token = first });

        complete.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task A_browser_with_its_own_nonce_cannot_complete_someone_elses_link()
    {
        var emailA = NewEmail();
        var requesterA = factory.CreateHttpsClient();
        await StartAsync(requesterA, emailA);
        var tokenA = factory.Emails.LatestTokenFor(emailA);

        var requesterB = factory.CreateHttpsClient();
        await StartAsync(requesterB, NewEmail());

        var completeByB = await requesterB.PostAsJsonAsync("/api/identity/email/complete", new { token = tokenA });

        await completeByB.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "identity.magic_link_other_browser");
        (await requesterA.PostAsJsonAsync("/api/identity/email/complete", new { token = tokenA })).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Start_sets_a_path_scoped_http_only_nonce_cookie()
    {
        var start = await StartAsync(factory.CreateHttpsClient(), NewEmail());

        var cookie = start.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith("bb_email_nonce=", StringComparison.Ordinal));
        cookie.ShouldContain("path=/api/identity/email");
        cookie.ShouldContain("httponly");
        cookie.ShouldContain("secure");
        cookie.ShouldContain("samesite=lax");
    }

    private static Task<HttpResponseMessage> StartAsync(HttpClient client, string email, string mode = "signin") =>
        client.PostAsJsonAsync("/api/identity/email/start", new { email, callbackUrl = EmailCallbackUrl, language = "en", timeZone = "UTC", mode });
}
