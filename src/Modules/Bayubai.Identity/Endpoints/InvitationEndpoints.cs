using System.Security.Claims;
using Bayubai.Identity.Domain;
using Bayubai.Identity.Persistence;
using Bayubai.Identity.Security;
using Bayubai.SharedKernel.Consultants;
using Bayubai.SharedKernel.Errors;
using Bayubai.SharedKernel.Validation;
using Bayubai.SharedKernel.Web;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NodaTime;

namespace Bayubai.Identity.Endpoints;

internal static class InvitationEndpoints
{
    public const string Pending = "pending";
    public const string Accepted = "accepted";
    public const string Expired = "expired";

    public static void MapInvitations(this RouteGroupBuilder group)
    {
        var consultant = group.MapGroup("").RequireAuthorization(IdentityPolicies.Consultant);
        consultant.MapPost("/invitations", CreateAsync).WithName("CreateInvitation");
        consultant.MapGet("/invitations", ListAsync).WithName("ListInvitations");
        consultant.MapGet("/clients", ListClientsAsync).WithName("ListClients");

        group.MapGet("/me/consultants", ListMyConsultantsAsync)
            .WithName("ListMyConsultants")
            .RequireAuthorization();

        group.MapPost("/invitations/accept", AcceptAsync)
            .WithName("AcceptInvitation")
            .RequireAuthorization()
            .WithRequestValidation<AcceptInvitationRequest>();
    }

    private static async Task<Ok<CreateInvitationResponse>> CreateAsync(
        ICurrentConsultant consultant,
        IdentityModuleDbContext db,
        IClock clock,
        IOptions<IdentityModuleOptions> identity,
        IOptions<FrontendOptions> frontend,
        CancellationToken cancellationToken)
    {
        var now = clock.GetCurrentInstant();
        var token = SecureTokens.Create();
        var invitation = new Invitation
        {
            Id = Guid.CreateVersion7(),
            // The consultant policy guarantees a consultant id here.
            ConsultantId = consultant.ConsultantId!.Value,
            TokenHash = token.Hash,
            CreatedAt = now,
            ExpiresAt = now + Duration.FromDays(identity.Value.InvitationLifetimeDays),
        };
        db.Invitations.Add(invitation);
        await db.SaveChangesAsync(cancellationToken);

        var url = $"{frontend.Value.ClientAppUrl.TrimEnd('/')}/invite/{token.Value}";
        return TypedResults.Ok(new CreateInvitationResponse(invitation.Id, url, invitation.ExpiresAt));
    }

    private static async Task<Ok<List<InvitationResponse>>> ListAsync(IdentityModuleDbContext db, IClock clock, CancellationToken cancellationToken)
    {
        var now = clock.GetCurrentInstant();
        var invitations = await db.Invitations.OrderByDescending(i => i.CreatedAt).ToListAsync(cancellationToken);
        return TypedResults.Ok(invitations
            .Select(i => new InvitationResponse(i.Id, i.CreatedAt, i.ExpiresAt, StatusOf(i, now)))
            .ToList());
    }

    private static async Task<Ok<List<ClientResponse>>> ListClientsAsync(IdentityModuleDbContext db, CancellationToken cancellationToken)
    {
        var clients = await (
            from link in db.ClientLinks
            join user in db.Users on link.ParentUserId equals user.Id
            orderby link.LinkedAt descending
            select new ClientResponse(user.Id, user.DisplayName, user.Language, user.TimeZone, link.LinkedAt))
            .ToListAsync(cancellationToken);
        return TypedResults.Ok(clients);
    }

    private static async Task<Ok<List<ConsultantResponse>>> ListMyConsultantsAsync(
        ClaimsPrincipal principal,
        IdentityModuleDbContext db,
        CancellationToken cancellationToken)
    {
        var userId = principal.GetUserId()!.Value;
        // The caller is the parent side of the link, which the consultant filter hides; the explicit parent id keeps this scoped.
        var consultants = await (
            from link in db.ClientLinks.IgnoreQueryFilters()
            join user in db.Users on link.ConsultantId equals user.Id
            where link.ParentUserId == userId
            orderby link.LinkedAt descending
            select new ConsultantResponse(user.Id, user.DisplayName, user.TimeZone, link.LinkedAt))
            .ToListAsync(cancellationToken);
        return TypedResults.Ok(consultants);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> AcceptAsync(
        AcceptInvitationRequest request,
        ClaimsPrincipal principal,
        IdentityModuleDbContext db,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var userId = principal.GetUserId()!.Value;
        var hash = SecureTokens.Hash(request.Token);
        // The caller is a parent, not the owning consultant, so the lookup is by secret token and bypasses the consultant filter.
        var invitation = await db.Invitations.IgnoreQueryFilters().AsNoTracking()
            .SingleOrDefaultAsync(i => i.TokenHash == hash, cancellationToken);
        var now = clock.GetCurrentInstant();
        if (invitation is null)
        {
            return IdentityErrors.InviteNotFound.ToProblem();
        }

        if (invitation.AcceptedAt is not null)
        {
            return IdentityErrors.InviteUsed.ToProblem();
        }

        if (invitation.ExpiresAt <= now)
        {
            return IdentityErrors.InviteExpired.ToProblem();
        }

        if (invitation.ConsultantId == userId)
        {
            return IdentityErrors.InviteOwn.ToProblem();
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        // Same reason as above; the conditional update also makes acceptance single-use under races.
        var claimed = await db.Invitations.IgnoreQueryFilters()
            .Where(i => i.Id == invitation.Id && i.AcceptedAt == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(i => i.AcceptedAt, (Instant?)now)
                .SetProperty(i => i.AcceptedByUserId, (Guid?)userId), cancellationToken);
        if (claimed == 0)
        {
            return IdentityErrors.InviteUsed.ToProblem();
        }

        // The caller is the parent, not the owning consultant, so the filter would hide every row; the pair of explicit ids keeps this scoped.
        var alreadyLinked = await db.ClientLinks.IgnoreQueryFilters()
            .AnyAsync(l => l.ConsultantId == invitation.ConsultantId && l.ParentUserId == userId, cancellationToken);
        if (!alreadyLinked)
        {
            db.ClientLinks.Add(new ClientLink { ConsultantId = invitation.ConsultantId, ParentUserId = userId, LinkedAt = now });
            await db.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static string StatusOf(Invitation invitation, Instant now) =>
        invitation.AcceptedAt is not null ? Accepted : invitation.ExpiresAt <= now ? Expired : Pending;
}
