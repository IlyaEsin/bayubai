namespace Bayubai.AppHost;

// The migrations job signs in as the server admin and creates this role; the API signs in as the role and can only read and write rows.
internal static class DatabaseAccess
{
    public const string AppRole = "bayubai_app";
    public const string Database = "bayubai";

    public static IResourceBuilder<ProjectResource> WithAppRoleSetup(
        this IResourceBuilder<ProjectResource> migrations, IResourceBuilder<ParameterResource> appRolePassword) =>
        migrations
            .WithEnvironment("Database__AppRole", AppRole)
            .WithEnvironment("Database__AppRolePassword", appRolePassword);
}
