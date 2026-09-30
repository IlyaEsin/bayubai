using System.Text.Json;
using System.Text.Json.Nodes;
using Bayubai.Api.IntegrationTests.Infrastructure;

namespace Bayubai.Api.IntegrationTests;

public class OpenApiContractTests(ApiFactory factory)
{
    // Set to 1 to rewrite the committed snapshot after an intended contract change.
    private const string UpdateVariable = "BAYUBAI_UPDATE_OPENAPI";

    [Fact]
    public async Task Committed_snapshot_matches_the_served_document()
    {
        var served = JsonNode.Parse(await factory.CreateHttpsClient().GetStringAsync("/openapi/v1.json"))!.AsObject();
        // The server URL depends on the host the document was fetched from.
        served.Remove("servers");
        var actual = served.ToJsonString(new JsonSerializerOptions { WriteIndented = true }).ReplaceLineEndings("\n") + "\n";
        var path = Path.Combine(RepositoryRoot(), "web", "packages", "api-client", "openapi.json");

        if (Environment.GetEnvironmentVariable(UpdateVariable) == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, actual);
        }

        File.Exists(path).ShouldBeTrue($"Missing {path}; run the tests once with {UpdateVariable}=1.");
        (await File.ReadAllTextAsync(path)).ReplaceLineEndings("\n")
            .ShouldBe(actual, $"The API contract changed; run the tests with {UpdateVariable}=1 and regenerate the web client.");
    }

    [Fact]
    public async Task Every_operation_has_an_id_and_every_error_code_is_published()
    {
        var document = JsonNode.Parse(await factory.CreateHttpsClient().GetStringAsync("/openapi/v1.json"))!;

        var operations = document["paths"]!.AsObject().SelectMany(path => path.Value!.AsObject()).ToList();
        operations.ShouldAllBe(operation => operation.Value!["operationId"] != null);
        var codes = document["components"]!["schemas"]!["ErrorCode"]!["enum"]!.AsArray().Select(code => code!.GetValue<string>()).ToList();
        codes.ShouldContain("validation_failed");
        codes.ShouldContain("identity.invite_expired");
        codes.ShouldContain("identity.magic_link_other_browser");
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Bayubai.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Bayubai.slnx not found above the test output directory.");
    }
}
