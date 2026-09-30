using System.Net;
using System.Net.Http.Json;
using Bayubai.Api.IntegrationTests.Infrastructure;
using Bayubai.Identity.Endpoints;
using Bayubai.Identity.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Bayubai.Api.IntegrationTests.Infrastructure.SignInExtensions;

namespace Bayubai.Api.IntegrationTests;

public class AccountDeletionTests(ApiFactory factory)
{
    [Fact]
    public async Task Deleting_a_parent_removes_the_user_methods_links_and_tokens()
    {
        var email = NewEmail();
        var consultant = await factory.CreateConsultantClientAsync();
        var parent = await factory.SignedInClientAsync(email);
        await parent.PostAsJsonAsync("/api/identity/telegram/complete", new
        {
            auth = TelegramPayload.Create(Random.Shared.NextInt64(1_000_000, long.MaxValue).ToString(System.Globalization.CultureInfo.InvariantCulture), "Anna", factory.Clock.GetCurrentInstant()),
            mode = "link",
        });
        var invitation = await (await consultant.PostAsync("/api/identity/invitations", null)).ReadAsAsync<CreateInvitationResponse>();
        await parent.PostAsJsonAsync("/api/identity/invitations/accept", new { token = invitation.Url.Split("/invite/")[1] });
        var parentId = (await parent.GetMeAsync()).Id;

        (await parent.DeleteAsync("/api/identity/me")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await parent.GetAsync("/api/identity/me")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>();
        (await db.Users.AnyAsync(u => u.Id == parentId)).ShouldBeFalse();
        (await db.UserLogins.AnyAsync(l => l.UserId == parentId)).ShouldBeFalse();
        (await db.UserRoles.AnyAsync(r => r.UserId == parentId)).ShouldBeFalse();
        (await db.ClientLinks.IgnoreQueryFilters().AnyAsync(l => l.ParentUserId == parentId)).ShouldBeFalse();
        (await db.MagicLinkTokens.AnyAsync(t => t.Email == email)).ShouldBeFalse();
        (await db.Invitations.IgnoreQueryFilters().AnyAsync(i => i.AcceptedByUserId == parentId)).ShouldBeFalse();
        (await (await consultant.GetAsync("/api/identity/clients")).ReadAsAsync<List<ClientResponse>>()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Deleting_a_consultant_removes_their_invitations_and_client_links()
    {
        var consultant = await factory.CreateConsultantClientAsync();
        var consultantId = (await consultant.GetMeAsync()).Id;
        var invitation = await (await consultant.PostAsync("/api/identity/invitations", null)).ReadAsAsync<CreateInvitationResponse>();
        var parent = await factory.SignedInClientAsync(NewEmail());
        await parent.PostAsJsonAsync("/api/identity/invitations/accept", new { token = invitation.Url.Split("/invite/")[1] });

        (await consultant.DeleteAsync("/api/identity/me")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>();
        (await db.Invitations.IgnoreQueryFilters().AnyAsync(i => i.ConsultantId == consultantId)).ShouldBeFalse();
        (await db.ClientLinks.IgnoreQueryFilters().AnyAsync(l => l.ConsultantId == consultantId)).ShouldBeFalse();
        (await parent.GetAsync("/api/identity/me")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Session_on_another_device_stops_working_after_deletion()
    {
        var email = NewEmail();
        var phone = await factory.SignedInClientAsync(email);
        var laptop = await factory.SignedInClientAsync(email);

        (await phone.DeleteAsync("/api/identity/me")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await laptop.GetAsync("/api/identity/me")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await laptop.PutAsJsonAsync("/api/identity/me", new { displayName = "Ghost", language = "en", timeZone = "UTC" }))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Consultant_session_on_another_device_stops_working_after_deletion()
    {
        var email = NewEmail("consultant");
        var admin = await factory.GetAdminClientAsync();
        (await admin.PostAsJsonAsync("/api/identity/admin/consultants", new { email, displayName = "Consultant", language = "ru", timeZone = "Europe/Moscow" }))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        var phone = await factory.SignedInClientAsync(email);
        var laptop = await factory.SignedInClientAsync(email);

        (await phone.DeleteAsync("/api/identity/me")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await laptop.GetAsync("/api/identity/clients")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await laptop.GetAsync("/api/identity/invitations")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await laptop.PostAsync("/api/identity/invitations", null)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
