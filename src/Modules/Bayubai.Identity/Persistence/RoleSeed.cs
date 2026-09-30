using Microsoft.AspNetCore.Identity;

namespace Bayubai.Identity.Persistence;

internal static class RoleSeed
{
    public static IdentityRole<Guid>[] Roles { get; } =
    [
        Create("0199a5c0-0000-7000-8000-000000000001", IdentityRoles.Parent),
        Create("0199a5c0-0000-7000-8000-000000000002", IdentityRoles.Consultant),
        Create("0199a5c0-0000-7000-8000-000000000003", IdentityRoles.Admin),
    ];

    // Fixed ids and stamps keep the seed stable across migrations.
    private static IdentityRole<Guid> Create(string id, string name) => new()
    {
        Id = Guid.Parse(id),
        Name = name,
        NormalizedName = name.ToUpperInvariant(),
        ConcurrencyStamp = id,
    };
}
