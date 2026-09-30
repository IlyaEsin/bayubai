using CareNest.Api.IntegrationTests.Infrastructure;
using CareNest.Identity;
using CareNest.Identity.Accounts;
using CareNest.Identity.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace CareNest.Api.IntegrationTests;

public class AccountServiceTests(ApiFactory factory)
{
    private static readonly NewUserDefaults Russian = new("ru", "Europe/Moscow");

    [Fact]
    public async Task New_identity_creates_parent_with_login_and_defaults()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var accounts = Accounts(scope);
        var users = Users(scope);
        var identity = NewIdentity("Anna");

        var outcome = await accounts.ResolveAsync(identity, SignInMode.SignIn, null, Russian, CancellationToken.None);

        var user = outcome.User.ShouldNotBeNull();
        user.DisplayName.ShouldBe("Anna");
        user.Language.ShouldBe("ru");
        user.TimeZone.ShouldBe("Europe/Moscow");
        (await users.GetRolesAsync(user)).ShouldBe(new[] { IdentityRoles.Parent });
        (await users.FindByLoginAsync(identity.Provider, identity.ProviderKey))!.Id.ShouldBe(user.Id);
    }

    [Fact]
    public async Task Known_identity_signs_in_the_same_user()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var accounts = Accounts(scope);
        var identity = NewIdentity("Anna");

        var first = await accounts.ResolveAsync(identity, SignInMode.SignIn, null, Russian, CancellationToken.None);
        var second = await accounts.ResolveAsync(identity, SignInMode.SignIn, null, Russian, CancellationToken.None);

        second.User!.Id.ShouldBe(first.User!.Id);
    }

    [Fact]
    public async Task Invalid_defaults_fall_back_to_english_and_utc()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var accounts = Accounts(scope);

        var outcome = await accounts.ResolveAsync(NewIdentity(null), SignInMode.SignIn, null, new NewUserDefaults("de", "Moscow"), CancellationToken.None);

        outcome.User!.Language.ShouldBe("en");
        outcome.User.TimeZone.ShouldBe("UTC");
        outcome.User.DisplayName.ShouldBe("");
    }

    [Fact]
    public async Task Link_adds_the_method_to_the_signed_in_user()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var accounts = Accounts(scope);
        var users = Users(scope);
        var owner = (await accounts.ResolveAsync(NewIdentity("Anna"), SignInMode.SignIn, null, Russian, CancellationToken.None)).User!;
        var second = NewIdentity("Anna");

        var outcome = await accounts.ResolveAsync(second, SignInMode.Link, owner.Id, Russian, CancellationToken.None);

        outcome.User!.Id.ShouldBe(owner.Id);
        (await users.GetLoginsAsync(owner)).Count.ShouldBe(2);
    }

    [Fact]
    public async Task Link_of_a_method_owned_by_someone_else_fails()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var accounts = Accounts(scope);
        var taken = NewIdentity("Other");
        await accounts.ResolveAsync(taken, SignInMode.SignIn, null, Russian, CancellationToken.None);
        var me = (await accounts.ResolveAsync(NewIdentity("Me"), SignInMode.SignIn, null, Russian, CancellationToken.None)).User!;

        var outcome = await accounts.ResolveAsync(taken, SignInMode.Link, me.Id, Russian, CancellationToken.None);

        outcome.Error.ShouldBe(IdentityErrors.LoginAlreadyLinked);
    }

    [Fact]
    public async Task Link_without_a_signed_in_user_fails()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var accounts = Accounts(scope);

        var outcome = await accounts.ResolveAsync(NewIdentity("Anna"), SignInMode.Link, null, Russian, CancellationToken.None);

        outcome.Error.ShouldBe(IdentityErrors.NotSignedIn);
    }

    [Fact]
    public async Task Ensure_consultant_creates_a_consultant_only_user_and_is_idempotent()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var accounts = Accounts(scope);
        var users = Users(scope);
        var email = $"Consultant-{Guid.NewGuid():N}@Example.Test";

        var created = await accounts.EnsureConsultantAsync(email, "Anna", Russian, CancellationToken.None);
        var again = await accounts.EnsureConsultantAsync(email.ToLowerInvariant(), "Anna", Russian, CancellationToken.None);

        again.Id.ShouldBe(created.Id);
        created.DisplayName.ShouldBe("Anna");
        (await users.GetRolesAsync(created)).ShouldBe(new[] { IdentityRoles.Consultant });
    }

    [Fact]
    public async Task Ensure_consultant_promotes_an_existing_parent()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var accounts = Accounts(scope);
        var users = Users(scope);
        var email = $"parent-{Guid.NewGuid():N}@example.test";
        var parent = (await accounts.ResolveAsync(new ExternalIdentity(EmailLogin.Provider, email, "P"), SignInMode.SignIn, null, Russian, CancellationToken.None)).User!;

        var consultant = await accounts.EnsureConsultantAsync(email, "P", Russian, CancellationToken.None);

        consultant.Id.ShouldBe(parent.Id);
        (await users.GetRolesAsync(consultant)).Order().ShouldBe(new[] { IdentityRoles.Consultant, IdentityRoles.Parent });
    }

    private static ExternalIdentity NewIdentity(string? name) => new("Google", Guid.NewGuid().ToString("N"), name);

    private static AccountService Accounts(AsyncServiceScope scope) => scope.ServiceProvider.GetRequiredService<AccountService>();

    private static UserManager<User> Users(AsyncServiceScope scope) => scope.ServiceProvider.GetRequiredService<UserManager<User>>();
}
