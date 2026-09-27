using CareNest.Identity.External;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace CareNest.Identity.Tests;

public class ExternalProvidersTests
{
    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void Fake_provider_refuses_to_register_in_a_deployed_environment(string environmentName) =>
        Should.Throw<InvalidOperationException>(() => Register(fakeEnabled: true, environmentName));

    [Theory]
    [InlineData("Development")]
    [InlineData("Testing")]
    public async Task Fake_provider_registers_locally_only_when_enabled(string environmentName)
    {
        (await SchemeAsync(Register(fakeEnabled: true, environmentName))).ShouldNotBeNull();
        (await SchemeAsync(Register(fakeEnabled: false, environmentName))).ShouldBeNull();
    }

    private static IServiceCollection Register(bool fakeEnabled, string environmentName)
    {
        var services = new ServiceCollection();
        services.AddDataProtection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Identity:Providers:Fake:Enabled"] = fakeEnabled.ToString() })
            .Build();
        ExternalProviders.Register(services.AddAuthentication(), configuration, new TestEnvironment(environmentName));
        return services;
    }

    private static Task<AuthenticationScheme?> SchemeAsync(IServiceCollection services) =>
        services.BuildServiceProvider().GetRequiredService<IAuthenticationSchemeProvider>().GetSchemeAsync(ExternalProviders.Fake);

    private sealed class TestEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "CareNest.Identity.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
