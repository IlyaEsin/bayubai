using Bayubai.SharedKernel.Web;

namespace Bayubai.SharedKernel.Tests;

public class FrontendOptionsTests
{
    [Fact]
    public void Absolute_origins_and_client_url_are_valid() =>
        new FrontendOptions { Origins = ["https://app.example.test", "https://studio.example.test"], ClientAppUrl = "https://app.example.test" }
            .IsValid().ShouldBeTrue();

    [Theory]
    [InlineData("")]
    [InlineData("/app")]
    [InlineData("app.example.test")]
    [InlineData("ftp://app.example.test")]
    public void Missing_or_relative_client_url_is_invalid(string clientAppUrl) =>
        new FrontendOptions { Origins = ["https://app.example.test"], ClientAppUrl = clientAppUrl }.IsValid().ShouldBeFalse();

    [Fact]
    public void No_origins_is_invalid() =>
        new FrontendOptions { ClientAppUrl = "https://app.example.test" }.IsValid().ShouldBeFalse();
}
