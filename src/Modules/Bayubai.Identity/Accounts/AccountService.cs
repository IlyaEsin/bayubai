using Bayubai.Identity.Domain;
using Bayubai.Identity.Persistence;
using Bayubai.Identity.Security;
using Bayubai.SharedKernel.Localization;
using Bayubai.SharedKernel.Time;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using NodaTime;

namespace Bayubai.Identity.Accounts;

internal sealed class AccountService(
    UserManager<User> users,
    IdentityModuleDbContext db,
    IClock clock,
    IOptions<IdentityModuleOptions> options)
{
    public async Task<SignInOutcome> ResolveAsync(
        ExternalIdentity identity,
        SignInMode mode,
        Guid? currentUserId,
        NewUserDefaults defaults,
        CancellationToken cancellationToken)
    {
        var existing = await users.FindByLoginAsync(identity.Provider, identity.ProviderKey);
        if (mode == SignInMode.Link)
        {
            return await LinkAsync(identity, existing, currentUserId);
        }

        return SignInOutcome.Success(existing ?? await CreateUserAsync(identity, defaults, IdentityRoles.Parent, cancellationToken));
    }

    public async Task EnsureAdminRoleAsync(User user, string normalizedEmail)
    {
        var isAdminEmail = options.Value.AdminEmails.Any(email => EmailLogin.Normalize(email) == normalizedEmail);
        if (isAdminEmail && !await users.IsInRoleAsync(user, IdentityRoles.Admin))
        {
            (await users.AddToRoleAsync(user, IdentityRoles.Admin)).ThrowIfFailed();
        }
    }

    public async Task<User> EnsureConsultantAsync(string email, string displayName, NewUserDefaults defaults, CancellationToken cancellationToken)
    {
        var normalized = EmailLogin.Normalize(email);
        var user = await users.FindByLoginAsync(EmailLogin.Provider, normalized)
            ?? await CreateUserAsync(
                new ExternalIdentity(EmailLogin.Provider, normalized, displayName),
                defaults,
                IdentityRoles.Consultant,
                cancellationToken);

        if (!await users.IsInRoleAsync(user, IdentityRoles.Consultant))
        {
            (await users.AddToRoleAsync(user, IdentityRoles.Consultant)).ThrowIfFailed();
            // Existing sessions lack the new role; a new stamp makes them re-authenticate.
            (await users.UpdateSecurityStampAsync(user)).ThrowIfFailed();
        }

        return user;
    }

    private async Task<SignInOutcome> LinkAsync(ExternalIdentity identity, User? existing, Guid? currentUserId)
    {
        var current = currentUserId is { } id ? await users.FindByIdAsync(id.ToString()) : null;
        if (current is null)
        {
            return SignInOutcome.Failure(IdentityErrors.NotSignedIn);
        }

        if (existing is not null)
        {
            return existing.Id == current.Id ? SignInOutcome.Success(current) : SignInOutcome.Failure(IdentityErrors.LoginAlreadyLinked);
        }

        (await users.AddLoginAsync(current, new UserLoginInfo(identity.Provider, identity.ProviderKey, identity.Provider))).ThrowIfFailed();
        return SignInOutcome.Success(current);
    }

    private async Task<User> CreateUserAsync(ExternalIdentity identity, NewUserDefaults defaults, string role, CancellationToken cancellationToken)
    {
        var id = Guid.CreateVersion7();
        var user = new User
        {
            Id = id,
            UserName = id.ToString("N"),
            DisplayName = DisplayNames.Normalize(identity.DisplayName),
            Language = Languages.OrDefault(defaults.Language),
            TimeZone = TimeZones.OrDefault(defaults.TimeZone),
            CreatedAt = clock.GetCurrentInstant(),
        };

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        (await users.CreateAsync(user)).ThrowIfFailed();
        (await users.AddLoginAsync(user, new UserLoginInfo(identity.Provider, identity.ProviderKey, identity.Provider))).ThrowIfFailed();
        (await users.AddToRoleAsync(user, role)).ThrowIfFailed();
        await transaction.CommitAsync(cancellationToken);
        return user;
    }
}
