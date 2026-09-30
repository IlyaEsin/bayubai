using Bayubai.SharedKernel.Consultants;
using NodaTime;

namespace Bayubai.Identity.Domain;

internal sealed class Invitation : IConsultantOwned
{
    public Guid Id { get; set; }

    public Guid ConsultantId { get; set; }

    public required string TokenHash { get; set; }

    public Instant CreatedAt { get; set; }

    public Instant ExpiresAt { get; set; }

    public Instant? AcceptedAt { get; set; }

    public Guid? AcceptedByUserId { get; set; }
}
