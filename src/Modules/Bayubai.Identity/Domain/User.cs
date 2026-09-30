using Microsoft.AspNetCore.Identity;
using NodaTime;

namespace Bayubai.Identity.Domain;

internal sealed class User : IdentityUser<Guid>
{
    public required string DisplayName { get; set; }

    public required string Language { get; set; }

    public required string TimeZone { get; set; }

    public Instant CreatedAt { get; set; }
}
