using CareNest.Identity.Accounts;
using CareNest.Identity.Domain;
using CareNest.Identity.Email;
using CareNest.Identity.Persistence;
using CareNest.Identity.Security;
using CareNest.SharedKernel.Errors;
using CareNest.SharedKernel.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace CareNest.Identity.Endpoints;

internal static class EmailSignInEndpoints
{
    public static readonly Duration LinkLifetime = Duration.FromMinutes(15);
    public static readonly Duration ThrottleWindow = Duration.FromMinutes(10);
    public const int MaxLinksPerWindow = 3;
    public const string NonceCookie = "cn_email_nonce";
    public const string StartRateLimit = "identity.email-start";

    public static void MapEmailSignIn(this RouteGroupBuilder group)
    {
        group.MapPost("/email/start", StartAsync)
            .WithName("StartEmailSignIn")
            .WithRequestValidation<EmailStartRequest>()
            .RequireRateLimiting(StartRateLimit);
        group.MapPost("/email/complete", CompleteAsync).WithName("CompleteEmailSignIn").WithRequestValidation<EmailCompleteRequest>();
    }

    private static async Task<Results<Accepted, ProblemHttpResult>> StartAsync(
        EmailStartRequest request,
        HttpContext http,
        ReturnUrlPolicy returnUrls,
        IdentityModuleDbContext db,
        UserManager<User> users,
        IEmailSender sender,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (!returnUrls.IsAllowed(request.CallbackUrl))
        {
            return IdentityErrors.InvalidReturnUrl.ToProblem();
        }

        SignInModes.TryParse(request.Mode, out var mode);
        var linkUserId = mode == SignInMode.Link ? http.User.GetUserId() : null;
        if (mode == SignInMode.Link && linkUserId is null)
        {
            return IdentityErrors.NotSignedIn.ToProblem();
        }

        var email = EmailLogin.Normalize(request.Email);
        var nonce = BrowserNonce(http);
        var now = clock.GetCurrentInstant();
        var windowStart = now - ThrottleWindow;
        var token = SecureTokens.Create();
        var throttled = false;

        await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
        {
            // Serialises concurrent starts for the same address, so the count-then-insert below cannot race.
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({email}, 0))", cancellationToken);

            // The response is identical when throttled so the endpoint does not reveal anything about the address.
            throttled = await db.MagicLinkTokens.CountAsync(t => t.Email == email && t.CreatedAt > windowStart, cancellationToken) >= MaxLinksPerWindow;
            if (!throttled)
            {
                db.MagicLinkTokens.Add(new MagicLinkToken
                {
                    Id = Guid.CreateVersion7(),
                    TokenHash = token.Hash,
                    Email = email,
                    Language = request.Language,
                    TimeZone = request.TimeZone,
                    LinkUserId = linkUserId,
                    BrowserNonceHash = SecureTokens.Hash(nonce),
                    CreatedAt = now,
                    ExpiresAt = now + LinkLifetime,
                });
                await db.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }

        if (throttled)
        {
            return TypedResults.Accepted((string?)null);
        }

        var existing = await users.FindByLoginAsync(EmailLogin.Provider, email);
        var link = QueryHelpers.AddQueryString(request.CallbackUrl, "token", token.Value);
        await sender.SendAsync(MagicLinkEmail.Compose(email, existing?.Language ?? request.Language, link), cancellationToken);
        return TypedResults.Accepted((string?)null);
    }

    // Reuses the browser's nonce so a second request does not invalidate links already mailed.
    private static string BrowserNonce(HttpContext http)
    {
        var nonce = http.Request.Cookies[NonceCookie] is { Length: > 0 } existing ? existing : SecureTokens.Create().Value;
        http.Response.Cookies.Append(NonceCookie, nonce, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/api/identity/email",
            MaxAge = LinkLifetime.ToTimeSpan(),
        });
        return nonce;
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> CompleteAsync(
        EmailCompleteRequest request,
        HttpContext http,
        IdentityModuleDbContext db,
        AccountService accounts,
        SignInManager<User> signIn,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var hash = SecureTokens.Hash(request.Token);
        var token = await db.MagicLinkTokens.AsNoTracking().SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        var now = clock.GetCurrentInstant();
        if (token is null)
        {
            return IdentityErrors.MagicLinkInvalid.ToProblem();
        }

        if (token.UsedAt is not null)
        {
            return IdentityErrors.MagicLinkUsed.ToProblem();
        }

        if (token.ExpiresAt <= now)
        {
            return IdentityErrors.MagicLinkExpired.ToProblem();
        }

        if (token.LinkUserId is not null && token.LinkUserId != http.User.GetUserId())
        {
            return IdentityErrors.LinkSessionMismatch.ToProblem();
        }

        // Stops login CSRF: a link mailed to an attacker's address cannot sign in a victim's browser.
        if (http.Request.Cookies[NonceCookie] is not { Length: > 0 } nonce || SecureTokens.Hash(nonce) != token.BrowserNonceHash)
        {
            return IdentityErrors.MagicLinkOtherBrowser.ToProblem();
        }

        // The conditional update makes the token single-use even when two requests race.
        var claimed = await db.MagicLinkTokens
            .Where(t => t.Id == token.Id && t.UsedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.UsedAt, (Instant?)now), cancellationToken);
        if (claimed == 0)
        {
            return IdentityErrors.MagicLinkUsed.ToProblem();
        }

        var outcome = await accounts.ResolveAsync(
            new ExternalIdentity(EmailLogin.Provider, token.Email, EmailLogin.DisplayNameFrom(token.Email)),
            token.LinkUserId is null ? SignInMode.SignIn : SignInMode.Link,
            token.LinkUserId,
            new NewUserDefaults(token.Language, token.TimeZone),
            cancellationToken);
        if (outcome.Error is not null)
        {
            return outcome.Error.ToProblem();
        }

        await accounts.EnsureAdminRoleAsync(outcome.User!, token.Email);
        await signIn.SignInAsync(outcome.User!, isPersistent: true);
        return TypedResults.NoContent();
    }
}
