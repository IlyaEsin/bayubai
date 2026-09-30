using Bayubai.Identity.Domain;
using Bayubai.SharedKernel.Errors;

namespace Bayubai.Identity.Accounts;

internal enum SignInMode
{
    SignIn,
    Link,
}

internal static class SignInModes
{
    public const string SignIn = "signin";
    public const string Link = "link";

    public static bool TryParse(string? value, out SignInMode mode)
    {
        switch (value)
        {
            case null or SignIn:
                mode = SignInMode.SignIn;
                return true;
            case Link:
                mode = SignInMode.Link;
                return true;
            default:
                mode = SignInMode.SignIn;
                return false;
        }
    }

    public static string ToName(SignInMode mode) => mode == SignInMode.Link ? Link : SignIn;
}

internal sealed record ExternalIdentity(string Provider, string ProviderKey, string? DisplayName);

internal sealed record NewUserDefaults(string Language, string TimeZone);

internal sealed record SignInOutcome(User? User, ApiError? Error)
{
    public static SignInOutcome Success(User user) => new(user, null);

    public static SignInOutcome Failure(ApiError error) => new(null, error);
}
