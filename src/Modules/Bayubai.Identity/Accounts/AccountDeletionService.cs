using Bayubai.Identity.Domain;
using Bayubai.Identity.Persistence;
using Bayubai.Identity.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Bayubai.Identity.Accounts;

// Foreign keys cascade logins, roles, client links and the consultant's own invitations (accepted invitations keep their row with AcceptedByUserId set to null); email-keyed tokens have no FK and are removed here.
internal sealed class AccountDeletionService(UserManager<User> users, IdentityModuleDbContext db)
{
    public async Task DeleteAsync(User user, CancellationToken cancellationToken)
    {
        var emails = (await users.GetLoginsAsync(user))
            .Where(login => login.LoginProvider == EmailLogin.Provider)
            .Select(login => login.ProviderKey)
            .ToList();

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.MagicLinkTokens.Where(token => emails.Contains(token.Email)).ExecuteDeleteAsync(cancellationToken);
        (await users.DeleteAsync(user)).ThrowIfFailed();
        await transaction.CommitAsync(cancellationToken);
    }
}
