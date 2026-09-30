using Bayubai.Identity.Security;
using Bayubai.SharedKernel.Web;
using Microsoft.Extensions.Options;

namespace Bayubai.Identity.Tests;

public class ReturnUrlPolicyTests
{
    private readonly ReturnUrlPolicy _policy = new(Options.Create(new FrontendOptions
    {
        Origins = ["https://app.example.test", "http://localhost:5173/"],
    }));

    [Theory]
    [InlineData("https://app.example.test/invite/abc")]
    [InlineData("https://APP.example.test/")]
    [InlineData("https://app.example.test:443/auth")]
    [InlineData("http://localhost:5173/auth/email")]
    public void Allows_urls_on_frontend_origins(string url) => _policy.IsAllowed(url).ShouldBeTrue();

    [Theory]
    [InlineData("https://app.example.test.evil.com/")]
    [InlineData("https://app.example.test@evil.com/")]
    [InlineData("https://evil.com/?next=https://app.example.test")]
    [InlineData("http://app.example.test/")]
    [InlineData("https://app.example.test:8443/")]
    [InlineData("//evil.com/")]
    [InlineData("/relative/path")]
    [InlineData("javascript:alert(1)")]
    [InlineData("")]
    [InlineData(null)]
    public void Rejects_everything_else(string? url) => _policy.IsAllowed(url).ShouldBeFalse();
}
