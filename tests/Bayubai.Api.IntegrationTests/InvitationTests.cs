using System.Net;
using System.Net.Http.Json;
using Bayubai.Api.IntegrationTests.Infrastructure;
using Bayubai.Identity;
using Bayubai.Identity.Endpoints;
using NodaTime;
using static Bayubai.Api.IntegrationTests.Infrastructure.SignInExtensions;

namespace Bayubai.Api.IntegrationTests;

public class InvitationTests(ApiFactory factory)
{
    [Fact]
    public async Task Admin_creates_a_consultant_who_signs_in_with_the_consultant_role()
    {
        var consultant = await factory.CreateConsultantClientAsync();

        (await consultant.GetMeAsync()).Roles.ShouldBe(new[] { IdentityRoles.Consultant });
    }

    [Fact]
    public async Task Non_admin_cannot_create_consultants()
    {
        var parent = await factory.SignedInClientAsync(NewEmail());

        var response = await parent.PostAsJsonAsync("/api/identity/admin/consultants", new { email = NewEmail(), displayName = "X" });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Consultant_cannot_create_consultants()
    {
        var consultant = await factory.CreateConsultantClientAsync();

        var response = await consultant.PostAsJsonAsync("/api/identity/admin/consultants", new { email = NewEmail(), displayName = "X" });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Parent_cannot_use_consultant_endpoints()
    {
        var parent = await factory.SignedInClientAsync(NewEmail());

        (await parent.PostAsync("/api/identity/invitations", null)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await parent.GetAsync("/api/identity/invitations")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await parent.GetAsync("/api/identity/clients")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task New_parent_accepting_an_invitation_becomes_a_client()
    {
        var consultant = await factory.CreateConsultantClientAsync();
        var invitation = await CreateInvitationAsync(consultant);
        invitation.Url.ShouldStartWith(ApiFactory.ClientAppUrl + "/invite/");
        invitation.ExpiresAt.ShouldBe(factory.Clock.GetCurrentInstant() + Duration.FromDays(14));
        var parent = await factory.SignedInClientAsync(NewEmail());

        (await AcceptAsync(parent, invitation)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var clients = await (await consultant.GetAsync("/api/identity/clients")).ReadAsAsync<List<ClientResponse>>();
        clients.Select(c => c.UserId).ShouldBe(new[] { (await parent.GetMeAsync()).Id });
        var invitations = await (await consultant.GetAsync("/api/identity/invitations")).ReadAsAsync<List<InvitationResponse>>();
        invitations.Single().Status.ShouldBe("accepted");
    }

    [Fact]
    public async Task Client_list_shows_the_parent_language_and_time_zone()
    {
        // The consultant is created with Russian; the parent chose English, so both sides differ.
        var consultant = await factory.CreateConsultantClientAsync();
        var invitation = await CreateInvitationAsync(consultant);
        var parent = factory.CreateHttpsClient();
        await parent.SignInWithEmailAsync(factory, NewEmail(), language: "en", timeZone: "Asia/Vladivostok");

        await AcceptAsync(parent, invitation);

        var client = (await (await consultant.GetAsync("/api/identity/clients")).ReadAsAsync<List<ClientResponse>>()).Single();
        client.Language.ShouldBe("en");
        client.TimeZone.ShouldBe("Asia/Vladivostok");
        (await consultant.GetMeAsync()).Language.ShouldBe("ru");
    }

    [Fact]
    public async Task Admin_sets_the_new_consultant_language_and_time_zone()
    {
        var consultant = await factory.CreateConsultantClientAsync(timeZone: "Asia/Yekaterinburg");

        var me = await consultant.GetMeAsync();
        me.TimeZone.ShouldBe("Asia/Yekaterinburg");
        me.Language.ShouldBe("ru");
    }

    [Fact]
    public async Task Parent_sees_linked_consultants_with_their_time_zone_and_nobody_else()
    {
        var consultant = await factory.CreateConsultantClientAsync(timeZone: "Europe/Moscow");
        var invitation = await CreateInvitationAsync(consultant);
        var parent = await factory.SignedInClientAsync(NewEmail());
        await AcceptAsync(parent, invitation);
        var otherParent = await factory.SignedInClientAsync(NewEmail());

        var mine = await (await parent.GetAsync("/api/identity/me/consultants")).ReadAsAsync<List<ConsultantResponse>>();
        var others = await (await otherParent.GetAsync("/api/identity/me/consultants")).ReadAsAsync<List<ConsultantResponse>>();

        mine.Single().UserId.ShouldBe((await consultant.GetMeAsync()).Id);
        mine.Single().TimeZone.ShouldBe("Europe/Moscow");
        others.ShouldBeEmpty();
    }

    [Fact]
    public async Task Used_invitation_is_rejected()
    {
        var consultant = await factory.CreateConsultantClientAsync();
        var invitation = await CreateInvitationAsync(consultant);
        await AcceptAsync(await factory.SignedInClientAsync(NewEmail()), invitation);

        var second = await AcceptAsync(await factory.SignedInClientAsync(NewEmail()), invitation);

        await second.ShouldBeProblemAsync(HttpStatusCode.Conflict, "identity.invite_used");
    }

    [Fact]
    public async Task Expired_invitation_is_rejected_and_listed_as_expired()
    {
        var consultant = await factory.CreateConsultantClientAsync();
        var invitation = await CreateInvitationAsync(consultant);
        var parent = await factory.SignedInClientAsync(NewEmail());
        factory.Clock.Advance(Duration.FromDays(14));

        var response = await AcceptAsync(parent, invitation);

        await response.ShouldBeProblemAsync(HttpStatusCode.Gone, "identity.invite_expired");
        var invitations = await (await consultant.GetAsync("/api/identity/invitations")).ReadAsAsync<List<InvitationResponse>>();
        invitations.Single().Status.ShouldBe("expired");
    }

    [Fact]
    public async Task Unknown_invitation_is_not_found()
    {
        var parent = await factory.SignedInClientAsync(NewEmail());

        var response = await parent.PostAsJsonAsync("/api/identity/invitations/accept", new { token = "unknown" });

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "identity.invite_not_found");
    }

    [Fact]
    public async Task Consultant_cannot_accept_their_own_invitation()
    {
        var consultant = await factory.CreateConsultantClientAsync();
        var invitation = await CreateInvitationAsync(consultant);

        var response = await AcceptAsync(consultant, invitation);

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "identity.invite_own");
    }

    [Fact]
    public async Task Second_invitation_from_the_same_consultant_keeps_one_client_link()
    {
        var consultant = await factory.CreateConsultantClientAsync();
        var parent = await factory.SignedInClientAsync(NewEmail());
        await AcceptAsync(parent, await CreateInvitationAsync(consultant));

        (await AcceptAsync(parent, await CreateInvitationAsync(consultant))).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var clients = await (await consultant.GetAsync("/api/identity/clients")).ReadAsAsync<List<ClientResponse>>();
        clients.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Consultant_cannot_see_another_consultants_invitations_or_clients()
    {
        var consultantA = await factory.CreateConsultantClientAsync();
        var consultantB = await factory.CreateConsultantClientAsync();
        await AcceptAsync(await factory.SignedInClientAsync(NewEmail()), await CreateInvitationAsync(consultantA));

        (await (await consultantB.GetAsync("/api/identity/invitations")).ReadAsAsync<List<InvitationResponse>>()).ShouldBeEmpty();
        (await (await consultantB.GetAsync("/api/identity/clients")).ReadAsAsync<List<ClientResponse>>()).ShouldBeEmpty();
        (await (await consultantA.GetAsync("/api/identity/invitations")).ReadAsAsync<List<InvitationResponse>>()).Count.ShouldBe(1);
        (await (await consultantA.GetAsync("/api/identity/clients")).ReadAsAsync<List<ClientResponse>>()).Count.ShouldBe(1);
    }

    private static async Task<CreateInvitationResponse> CreateInvitationAsync(HttpClient consultant)
    {
        var response = await consultant.PostAsync("/api/identity/invitations", null);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.ReadAsAsync<CreateInvitationResponse>();
    }

    private static Task<HttpResponseMessage> AcceptAsync(HttpClient parent, CreateInvitationResponse invitation) =>
        parent.PostAsJsonAsync("/api/identity/invitations/accept", new { token = invitation.Url.Split("/invite/")[1] });
}
