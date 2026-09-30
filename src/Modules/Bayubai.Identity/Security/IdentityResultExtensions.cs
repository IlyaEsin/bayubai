using Microsoft.AspNetCore.Identity;

namespace Bayubai.Identity.Security;

internal static class IdentityResultExtensions
{
    // Identity error codes carry no personal data, so they are safe in the exception message.
    public static void ThrowIfFailed(this IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException("Identity operation failed: " + string.Join(", ", result.Errors.Select(error => error.Code)));
        }
    }
}
