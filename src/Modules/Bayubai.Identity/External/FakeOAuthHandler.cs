using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bayubai.Identity.External;

internal sealed class FakeOAuthOptions : RemoteAuthenticationOptions
{
    public FakeOAuthOptions()
    {
        CallbackPath = "/api/identity/signin-fake";
        // A custom remote scheme gets no events object unless it creates one.
        Events = new RemoteAuthenticationEvents();
    }

    public ISecureDataFormat<AuthenticationProperties> StateDataFormat { get; set; } = null!;
}

// Stands in for Google, Yandex ID and VK ID in local runs and e2e tests: the round trip is real, only the provider page is skipped.
internal sealed class FakeOAuthHandler(IOptionsMonitor<FakeOAuthOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : RemoteAuthenticationHandler<FakeOAuthOptions>(options, logger, encoder)
{
    public const string SubjectCookie = "cn_fake_subject";
    public const string DefaultSubject = "fake-user";

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        GenerateCorrelationId(properties);
        var subject = Request.Cookies[SubjectCookie] is { Length: > 0 } value ? value : DefaultSubject;
        var callback = QueryHelpers.AddQueryString(BuildRedirectUri(Options.CallbackPath), new Dictionary<string, string?>
        {
            ["state"] = Options.StateDataFormat.Protect(properties),
            ["subject"] = subject,
        });
        Response.Redirect(callback);
        return Task.CompletedTask;
    }

    protected override Task<HandleRequestResult> HandleRemoteAuthenticateAsync()
    {
        var properties = Options.StateDataFormat.Unprotect(Request.Query["state"]);
        if (properties is null)
        {
            return Task.FromResult(HandleRequestResult.Fail("The fake provider state is missing or invalid."));
        }

        if (!ValidateCorrelationId(properties))
        {
            return Task.FromResult(HandleRequestResult.Fail("Correlation failed.", properties));
        }

        var subject = Request.Query["subject"].ToString();
        if (subject.Length == 0)
        {
            return Task.FromResult(HandleRequestResult.Fail("The fake provider returned no subject.", properties));
        }

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, subject), new Claim(ClaimTypes.Name, subject)],
            Scheme.Name);
        return Task.FromResult(HandleRequestResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), properties, Scheme.Name)));
    }
}
