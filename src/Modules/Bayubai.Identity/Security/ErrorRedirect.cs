using Bayubai.SharedKernel.Errors;
using Microsoft.AspNetCore.WebUtilities;

namespace Bayubai.Identity.Security;

internal static class ErrorRedirect
{
    public static string For(string returnUrl, ApiError error) => QueryHelpers.AddQueryString(returnUrl, "error", error.Code);
}
