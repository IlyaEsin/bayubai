using System.Net;
using CareNest.Api.IntegrationTests.Infrastructure;
using CareNest.Identity.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CareNest.Api.IntegrationTests;

public class ProductionHostingTests(ApiFactory factory)
{
    [Fact]
    public async Task Liveness_is_served_outside_development_but_the_health_report_is_not()
    {
        var client = factory.CreateHttpsClient();

        (await client.GetAsync("/alive")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync("/health")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Forwarded_https_scheme_reaches_the_oauth_redirect_uri()
    {
        // The ingress talks plain http to the container and reports the original scheme in X-Forwarded-Proto.
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("http://localhost"), AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"/api/identity/external/Google/start?returnUrl={Uri.EscapeDataString(ApiFactory.ClientAppUrl + "/")}&mode=signin&language=en&timeZone=UTC");
        request.Headers.Add("X-Forwarded-Proto", "https");

        var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.Query.ShouldContain("redirect_uri=https%3A%2F%2Flocalhost%2Fapi%2Fidentity%2Fsignin-google");
    }

    [Fact]
    public async Task Data_protection_keys_are_stored_in_the_database()
    {
        factory.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector("test").Protect("payload");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityModuleDbContext>();
        (await db.DataProtectionKeys.CountAsync()).ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Host_refuses_to_start_without_an_absolute_client_app_url()
    {
        using var broken = factory.WithWebHostBuilder(builder => builder.UseSetting("Frontend:ClientAppUrl", "/app"));

        Should.Throw<OptionsValidationException>(() => broken.CreateClient());
    }
}
