using AspNet.Security.OAuth.VkId;
using CareNest.Identity.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CareNest.Identity.External;

internal static class ExternalProviders
{
    public const string Google = "Google";
    public const string Yandex = "Yandex";
    public const string VkId = "VkId";
    public const string Fake = "Fake";
    public const string Telegram = "Telegram";
    public const string TestingEnvironment = "Testing";

    public const string ReturnUrlItem = "cn.returnUrl";
    public const string ModeItem = "cn.mode";
    public const string LanguageItem = "cn.language";
    public const string TimeZoneItem = "cn.timeZone";

    public static IReadOnlyList<string> OAuth { get; } = [Google, Yandex, VkId, Fake];

    private sealed record Credentials(string ClientId, string ClientSecret);

    // Only configured providers are registered: an OAuth scheme without a ClientId breaks OpenAPI generation (verified 2026-09-24).
    public static void Register(AuthenticationBuilder authentication, IConfiguration configuration, IHostEnvironment environment)
    {
        if (Read(configuration, Google) is { } google)
        {
            authentication.AddGoogle(Google, options => Configure(options, google, Google));
        }

        if (Read(configuration, Yandex) is { } yandex)
        {
            authentication.AddYandex(Yandex, options => Configure(options, yandex, Yandex));
        }

        if (Read(configuration, VkId) is { } vk)
        {
            authentication.AddVkId(VkId, options => Configure(options, vk, VkId));
        }

        if (configuration.GetValue<bool>($"{IdentityModuleOptions.Section}:Providers:{Fake}:Enabled"))
        {
            // An allow-list, so a staging or any other deployed environment cannot get the test sign-in by a stray setting.
            if (!environment.IsDevelopment() && !environment.IsEnvironment(TestingEnvironment))
            {
                throw new InvalidOperationException("The fake sign-in provider is allowed only in Development and Testing.");
            }

            authentication.AddRemoteScheme<FakeOAuthOptions, FakeOAuthHandler>(Fake, Fake, ConfigureRemote);
            authentication.Services.AddOptions<FakeOAuthOptions>(Fake).Configure<IDataProtectionProvider>((options, protection) =>
                options.StateDataFormat = new PropertiesDataFormat(protection.CreateProtector(nameof(FakeOAuthHandler), Fake)));
        }
    }

    private static Credentials? Read(IConfiguration configuration, string provider)
    {
        var section = configuration.GetSection($"{IdentityModuleOptions.Section}:Providers:{provider}");
        var clientId = section["ClientId"];
        var clientSecret = section["ClientSecret"];
        return string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret) ? null : new Credentials(clientId, clientSecret);
    }

    private static void Configure(OAuthOptions options, Credentials credentials, string provider)
    {
        options.ClientId = credentials.ClientId;
        options.ClientSecret = credentials.ClientSecret;
        // Under /api so the Vite dev proxy forwards it and production needs one route prefix only.
        options.CallbackPath = $"/api/identity/signin-{provider.ToLowerInvariant()}";
        // VK ID requires PKCE; the others accept it.
        options.UsePkce = true;
        ConfigureRemote(options);
    }

    private static void ConfigureRemote(RemoteAuthenticationOptions options)
    {
        options.SignInScheme = IdentityConstants.ExternalScheme;
        // Every provider returns by a top-level GET redirect, which Lax cookies survive, and Lax also works over the plain-http local proxy.
        options.CorrelationCookie.SameSite = SameSiteMode.Lax;
        options.Events.OnRemoteFailure = context =>
        {
            var returnUrl = context.Properties?.GetString(ReturnUrlItem);
            context.Response.Redirect(returnUrl is null ? "/" : ErrorRedirect.For(returnUrl, IdentityErrors.ExternalLoginFailed));
            context.HandleResponse();
            return Task.CompletedTask;
        };
    }
}
