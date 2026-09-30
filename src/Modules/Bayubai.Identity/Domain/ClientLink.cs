using Bayubai.SharedKernel.Consultants;
using NodaTime;

namespace Bayubai.Identity.Domain;

internal sealed class ClientLink : IConsultantOwned
{
    public Guid ConsultantId { get; set; }

    public Guid ParentUserId { get; set; }

    public Instant LinkedAt { get; set; }
}
