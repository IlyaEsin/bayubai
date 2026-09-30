using System.Reflection;
using System.Text.RegularExpressions;
using Bayubai.SharedKernel.Errors;
using NetArchTest.Rules;

namespace Bayubai.ArchitectureTests;

public class ArchitectureTests
{
    private static readonly Assembly Host = typeof(Program).Assembly;
    private static readonly Assembly SharedKernel = typeof(ApiError).Assembly;

    // Every Bayubai assembly the host references, other than the kernel and Aspire defaults, is a module.
    private static readonly Assembly[] Modules = Host.GetReferencedAssemblies()
        .Where(name => name.Name!.StartsWith("Bayubai.", StringComparison.Ordinal)
            && name.Name is not "Bayubai.SharedKernel" and not "Bayubai.ServiceDefaults")
        .Select(Assembly.Load)
        .ToArray();

    [Fact]
    public void Identity_is_discovered_as_a_module() =>
        Modules.Select(module => module.GetName().Name).ShouldContain("Bayubai.Identity");

    [Fact]
    public void SharedKernel_depends_on_no_module_or_host()
    {
        var forbidden = Modules.Select(module => module.GetName().Name!).Append(Host.GetName().Name!).ToArray();

        var result = Types.InAssembly(SharedKernel).ShouldNot().HaveDependencyOnAny(forbidden).GetResult();

        result.IsSuccessful.ShouldBeTrue(Describe(result));
    }

    [Fact]
    public void Modules_depend_on_neither_each_other_nor_the_host()
    {
        foreach (var module in Modules)
        {
            var forbidden = Modules.Where(other => other != module).Select(other => other.GetName().Name!).Append(Host.GetName().Name!).ToArray();

            var result = Types.InAssembly(module).ShouldNot().HaveDependencyOnAny(forbidden).GetResult();

            result.IsSuccessful.ShouldBeTrue(Describe(result));
        }
    }

    [Fact]
    public void Modules_expose_public_types_only_in_their_root_namespace()
    {
        foreach (var module in Modules)
        {
            var root = module.GetName().Name!;

            var result = Types.InAssembly(module)
                .That().ArePublic()
                .And().DoNotResideInNamespace($"{root}.Persistence.Migrations")
                .Should().ResideInNamespaceMatching($"^{Regex.Escape(root)}$")
                .GetResult();

            result.IsSuccessful.ShouldBeTrue(Describe(result));
        }
    }

    [Fact]
    public void Kernel_and_module_code_uses_NodaTime_instead_of_DateTime()
    {
        foreach (var assembly in Modules.Prepend(SharedKernel))
        {
            var result = Types.InAssembly(assembly)
                .That().DoNotResideInNamespace($"{assembly.GetName().Name}.Persistence.Migrations")
                .ShouldNot().HaveDependencyOnAny("System.DateTime", "System.DateTimeOffset")
                .GetResult();

            result.IsSuccessful.ShouldBeTrue(Describe(result));
        }
    }

    [Fact]
    public void Host_kernel_and_modules_use_no_Azure_SDK()
    {
        // Portability: Azure reaches the app only as configuration, so a move to another host is a redeploy.
        foreach (var assembly in Modules.Append(SharedKernel).Append(Host))
        {
            var result = Types.InAssembly(assembly).ShouldNot().HaveDependencyOnAny("Azure", "Microsoft.Azure").GetResult();

            result.IsSuccessful.ShouldBeTrue(Describe(result));
        }
    }

    private static string Describe(NetArchTest.Rules.TestResult result) =>
        "Violations: " + string.Join(", ", result.FailingTypeNames ?? []);
}
