using NodaTime;

namespace Bayubai.Identity.Domain;

internal sealed class MagicLinkToken
{
    public Guid Id { get; set; }

    public required string TokenHash { get; set; }

    public required string Email { get; set; }

    public required string Language { get; set; }

    public required string TimeZone { get; set; }

    // Set when a signed-in user adds this email as a sign-in method; only that user may complete it.
    public Guid? LinkUserId { get; set; }

    // Hash of the nonce cookie of the browser that asked for the link; only that browser may complete it.
    public string? BrowserNonceHash { get; set; }

    public Instant CreatedAt { get; set; }

    public Instant ExpiresAt { get; set; }

    public Instant? UsedAt { get; set; }
}
