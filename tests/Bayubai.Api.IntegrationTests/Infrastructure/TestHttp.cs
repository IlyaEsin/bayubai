using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;

namespace Bayubai.Api.IntegrationTests.Infrastructure;

internal static class TestHttp
{
    public static JsonSerializerOptions Json { get; } =
        new JsonSerializerOptions(JsonSerializerDefaults.Web).ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);

    public static async Task<T> ReadAsAsync<T>(this HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(Json))!;

    public static async Task ShouldBeProblemAsync(this HttpResponseMessage response, HttpStatusCode status, string code)
    {
        response.StatusCode.ShouldBe(status);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("code").GetString().ShouldBe(code);
    }
}
