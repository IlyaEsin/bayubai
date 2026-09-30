using System.Reflection;
using Bayubai.SharedKernel.Errors;
using Microsoft.AspNetCore.Http;

namespace Bayubai.Identity;

internal static class IdentityErrors
{
    public static readonly ApiError InvalidReturnUrl = new("identity.invalid_return_url", StatusCodes.Status400BadRequest);
    public static readonly ApiError InvalidSignInMode = new("identity.invalid_sign_in_mode", StatusCodes.Status400BadRequest);
    public static readonly ApiError ProviderUnavailable = new("identity.provider_unavailable", StatusCodes.Status404NotFound);
    public static readonly ApiError ExternalLoginFailed = new("identity.external_login_failed", StatusCodes.Status400BadRequest);
    public static readonly ApiError LoginAlreadyLinked = new("identity.login_already_linked", StatusCodes.Status409Conflict);
    public static readonly ApiError NotSignedIn = new("identity.not_signed_in", StatusCodes.Status401Unauthorized);
    public static readonly ApiError LinkSessionMismatch = new("identity.link_session_mismatch", StatusCodes.Status403Forbidden);
    public static readonly ApiError MagicLinkInvalid = new("identity.magic_link_invalid", StatusCodes.Status400BadRequest);
    public static readonly ApiError MagicLinkUsed = new("identity.magic_link_used", StatusCodes.Status409Conflict);
    public static readonly ApiError MagicLinkExpired = new("identity.magic_link_expired", StatusCodes.Status410Gone);
    public static readonly ApiError InviteNotFound = new("identity.invite_not_found", StatusCodes.Status404NotFound);
    public static readonly ApiError InviteUsed = new("identity.invite_used", StatusCodes.Status409Conflict);
    public static readonly ApiError InviteExpired = new("identity.invite_expired", StatusCodes.Status410Gone);
    public static readonly ApiError InviteOwn = new("identity.invite_own", StatusCodes.Status400BadRequest);
    public static readonly ApiError MagicLinkOtherBrowser = new("identity.magic_link_other_browser", StatusCodes.Status403Forbidden);

    // Lazy, so this cannot read as null at type initialisation regardless of where a new error field is declared.
    private static readonly Lazy<IReadOnlyList<string>> LazyCodes = new(() => typeof(IdentityErrors)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.FieldType == typeof(ApiError))
        .Select(field => ((ApiError)field.GetValue(null)!).Code)
        .ToList());

    // Read by reflection so a new error cannot be left out of the OpenAPI ErrorCode enum.
    public static IReadOnlyList<string> Codes => LazyCodes.Value;
}
