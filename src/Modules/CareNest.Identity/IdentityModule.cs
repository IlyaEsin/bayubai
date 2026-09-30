using System.Threading.RateLimiting;
using CareNest.Identity.Accounts;
using CareNest.Identity.Domain;
using CareNest.Identity.Email;
using CareNest.Identity.Endpoints;
using CareNest.Identity.External;
using CareNest.Identity.Persistence;
using CareNest.Identity.Security;
using CareNest.SharedKernel.Consultants;
using CareNest.SharedKernel.Web;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CareNest.Identity;

public static class IdentityModule
{
    public const string ConnectionStringName = "carenest";

    public static IReadOnlyList<string> ErrorCodes => IdentityErrors.Codes;

    public static IHostApplicationBuilder AddIdentityModule(this IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        services.AddOptions<IdentityModuleOptions>().Bind(builder.Configuration.GetSection(IdentityModuleOptions.Section));
        services.AddOptions<FrontendOptions>()
            .Bind(builder.Configuration.GetSection(FrontendOptions.Section))
            .Validate(frontend => frontend.IsValid(), "Frontend:Origins and Frontend:ClientAppUrl must be absolute http(s) URLs.")
            .ValidateOnStart();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentConsultant, HttpCurrentConsultant>();
        builder.AddIdentityPersistence();

        // Keys live in the database so sessions and OAuth state survive restarts and are shared by every replica; an explicit store also wins over the Container Apps one.
        services.AddDataProtection()
            .SetApplicationName("CareNest")
            .PersistKeysToDbContext<IdentityModuleDbContext>();

        services.AddIdentityCore<User>()
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<IdentityModuleDbContext>()
            .AddSignInManager();
        // Zero means every request re-checks the user and security stamp, so a deleted or role-changed account cannot keep acting on a cached cookie.
        services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.Zero);

        var authentication = services.AddAuthentication(IdentityConstants.ApplicationScheme);
        authentication.AddIdentityCookies();
        ExternalProviders.Register(authentication, builder.Configuration, builder.Environment);
        services.ConfigureApplicationCookie(ConfigureSessionCookie);
        services.AddOptions<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme)
            .Configure<IOptions<IdentityModuleOptions>>((cookie, identity) => cookie.Cookie.Domain = identity.Value.CookieDomain);

        services.AddAuthorizationBuilder()
            .AddPolicy(IdentityPolicies.Consultant, policy => policy.RequireRole(IdentityRoles.Consultant))
            .AddPolicy(IdentityPolicies.Admin, policy => policy.RequireRole(IdentityRoles.Admin));

        services.AddSingleton<ReturnUrlPolicy>();
        services.AddScoped<AccountService>();
        services.AddScoped<AccountDeletionService>();

        services.AddOptions<EmailOptions>()
            .Bind(builder.Configuration.GetSection(EmailOptions.Section))
            .PostConfigure<IConfiguration>((email, configuration) =>
                email.ApplyConnectionString(configuration.GetConnectionString(EmailOptions.ConnectionStringName)));
        services.AddSingleton<IEmailSender, SmtpEmailSender>();

        services.AddRateLimiter(options => options.AddPolicy(EmailSignInEndpoints.StartRateLimit, http =>
            RateLimitPartition.GetFixedWindowLimiter(
                http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = http.RequestServices.GetRequiredService<IOptions<IdentityModuleOptions>>().Value.EmailStartsPerAddressWindow,
                    Window = EmailSignInEndpoints.ThrottleWindow.ToTimeSpan(),
                })));
        return builder;
    }

    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/identity").WithTags("Identity");
        group.MapEmailSignIn();
        group.MapProfile();
        group.MapExternalSignIn();
        group.MapAdmin();
        group.MapInvitations();
        return app;
    }

    public static IHostApplicationBuilder AddIdentityPersistence(this IHostApplicationBuilder builder)
    {
        builder.Services.TryAddScoped<ICurrentConsultant, NoCurrentConsultant>();
        builder.Services.AddDbContext<IdentityModuleDbContext>((services, options) =>
            options.UseNpgsql(
                services.GetRequiredService<IConfiguration>().GetConnectionString(ConnectionStringName),
                IdentityModuleDbContext.ConfigureNpgsql));
        // Retries are off so explicit transactions work without an execution strategy wrapper.
        builder.EnrichNpgsqlDbContext<IdentityModuleDbContext>(settings => settings.DisableRetry = true);
        return builder;
    }

    public static async Task MigrateIdentityDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>();
        await db.Database.MigrateAsync(cancellationToken);
    }

    private static void ConfigureSessionCookie(CookieAuthenticationOptions options)
    {
        options.Cookie.Name = "cn_session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
        // An API answers with status codes; redirects to a login page make no sense to a SPA.
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    }
}
