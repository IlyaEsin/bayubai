using System.Security.Claims;
using System.Text.Json;
using Bayubai.Identity.Accounts;
using Bayubai.Identity.Domain;
using Bayubai.Identity.External;
using Bayubai.Identity.Security;
using Bayubai.Identity.Telegram;
using Bayubai.SharedKernel.Errors;
using Bayubai.SharedKernel.Localization;
using Bayubai.SharedKernel.Time;
using Bayubai.SharedKernel.Validation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using NodaTime;

namespace Bayubai.Identity.Endpoints;

internal static class ExternalSignInEndpoints
{
    public const string CallbackPath = "/api/identity/external/callback";

    public static void MapExternalSignIn(this RouteGroupBuilder group)
    {
        group.MapGet("/providers", GetProvidersAsync).WithName("GetProviders");
        group.MapGet("/external/{provider}/start", StartAsync).WithName("StartExternalSignIn");
        group.MapGet("/external/callback", CallbackAsync).WithName("CompleteExternalSignIn");
        group.MapPost("/telegram/complete", TelegramAsync).WithName("CompleteTelegramSignIn").WithRequestValidation<TelegramCompleteRequest>();
    }

    private static async Task<Ok<ProvidersResponse>> GetProvidersAsync(IAuthenticationSchemeProvider schemes, IOptions<IdentityModuleOptions> options)
    {
        var providers = new List<string> { EmailLogin.Provider };
        foreach (var provider in ExternalProviders.OAuth)
        {
            if (await schemes.GetSchemeAsync(provider) is not null)
            {
                providers.Add(provider);
            }
        }

        if (!string.IsNullOrEmpty(options.Value.TelegramBotToken))
        {
            providers.Add(ExternalProviders.Telegram);
        }

        return TypedResults.Ok(new ProvidersResponse(providers, options.Value.TelegramBotName));
    }

    private static async Task<Results<ChallengeHttpResult, ProblemHttpResult>> StartAsync(
        string provider,
        string returnUrl,
        string? mode,
        string? language,
        string? timeZone,
        IAuthenticationSchemeProvider schemes,
        ReturnUrlPolicy returnUrls,
        SignInManager<User> signIn)
    {
        if (!ExternalProviders.OAuth.Contains(provider) || await schemes.GetSchemeAsync(provider) is null)
        {
            return IdentityErrors.ProviderUnavailable.ToProblem();
        }

        if (!returnUrls.IsAllowed(returnUrl))
        {
            return IdentityErrors.InvalidReturnUrl.ToProblem();
        }

        if (!SignInModes.TryParse(mode, out var signInMode))
        {
            return IdentityErrors.InvalidSignInMode.ToProblem();
        }

        var properties = signIn.ConfigureExternalAuthenticationProperties(provider, CallbackPath);
        properties.SetString(ExternalProviders.ReturnUrlItem, returnUrl);
        properties.SetString(ExternalProviders.ModeItem, SignInModes.ToName(signInMode));
        properties.SetString(ExternalProviders.LanguageItem, Languages.OrDefault(language));
        properties.SetString(ExternalProviders.TimeZoneItem, TimeZones.OrDefault(timeZone));
        return TypedResults.Challenge(properties, [provider]);
    }

    private static async Task<Results<RedirectHttpResult, ProblemHttpResult>> CallbackAsync(
        HttpContext http,
        SignInManager<User> signIn,
        AccountService accounts,
        ReturnUrlPolicy returnUrls,
        CancellationToken cancellationToken)
    {
        var info = await signIn.GetExternalLoginInfoAsync();
        await http.SignOutAsync(IdentityConstants.ExternalScheme);
        var properties = info?.AuthenticationProperties;
        var returnUrl = properties?.GetString(ExternalProviders.ReturnUrlItem);
        if (info is null || properties is null || !returnUrls.IsAllowed(returnUrl))
        {
            return IdentityErrors.ExternalLoginFailed.ToProblem();
        }

        SignInModes.TryParse(properties.GetString(ExternalProviders.ModeItem), out var mode);
        var outcome = await accounts.ResolveAsync(
            new ExternalIdentity(info.LoginProvider, info.ProviderKey, info.Principal.FindFirstValue(ClaimTypes.Name)),
            mode,
            http.User.GetUserId(),
            new NewUserDefaults(
                properties.GetString(ExternalProviders.LanguageItem) ?? Languages.Default,
                properties.GetString(ExternalProviders.TimeZoneItem) ?? TimeZones.Default),
            cancellationToken);
        if (outcome.Error is not null)
        {
            return TypedResults.Redirect(ErrorRedirect.For(returnUrl, outcome.Error));
        }

        await signIn.SignInAsync(outcome.User!, isPersistent: true);
        return TypedResults.Redirect(returnUrl);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> TelegramAsync(
        TelegramCompleteRequest request,
        HttpContext http,
        IOptions<IdentityModuleOptions> options,
        AccountService accounts,
        SignInManager<User> signIn,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var botToken = options.Value.TelegramBotToken;
        if (string.IsNullOrEmpty(botToken))
        {
            return IdentityErrors.ProviderUnavailable.ToProblem();
        }

        // Numbers keep their raw JSON text, which is exactly what Telegram signed.
        var fields = request.Auth.ToDictionary(
            field => field.Key,
            field => field.Value.ValueKind == JsonValueKind.String ? field.Value.GetString() ?? string.Empty : field.Value.GetRawText());
        var login = TelegramLoginVerifier.Verify(fields, botToken, clock.GetCurrentInstant());
        if (login is null)
        {
            return IdentityErrors.ExternalLoginFailed.ToProblem();
        }

        SignInModes.TryParse(request.Mode, out var mode);
        var outcome = await accounts.ResolveAsync(
            new ExternalIdentity(ExternalProviders.Telegram, login.Id, login.DisplayName),
            mode,
            http.User.GetUserId(),
            new NewUserDefaults(request.Language, request.TimeZone),
            cancellationToken);
        if (outcome.Error is not null)
        {
            return outcome.Error.ToProblem();
        }

        await signIn.SignInAsync(outcome.User!, isPersistent: true);
        return TypedResults.NoContent();
    }
}
