using System.Net;
using Bayubai.Api.IntegrationTests.Infrastructure;

namespace Bayubai.Api.IntegrationTests;

public class HostTests(ApiFactory factory)
{
    [Fact]
    public async Task OpenApi_document_is_served()
    {
        var response = await factory.CreateHttpsClient().GetAsync("/openapi/v1.json");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldContain("\"openapi\"");
    }

    [Fact]
    public async Task Scalar_reference_is_not_served_outside_development()
    {
        var response = await factory.CreateHttpsClient().GetAsync("/scalar");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Cors_allows_frontend_origin_with_credentials()
    {
        var response = await SendPreflightAsync(ApiFactory.ClientAppUrl);

        response.Headers.GetValues("Access-Control-Allow-Origin").ShouldBe(new[] { ApiFactory.ClientAppUrl });
        response.Headers.GetValues("Access-Control-Allow-Credentials").ShouldBe(new[] { "true" });
    }

    [Fact]
    public async Task Cors_ignores_unknown_origin()
    {
        var response = await SendPreflightAsync("https://evil.example");

        response.Headers.Contains("Access-Control-Allow-Origin").ShouldBeFalse();
    }

    private Task<HttpResponseMessage> SendPreflightAsync(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/openapi/v1.json");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        return factory.CreateHttpsClient().SendAsync(request);
    }
}
