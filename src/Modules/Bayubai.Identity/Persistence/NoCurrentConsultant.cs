using Bayubai.SharedKernel.Consultants;

namespace Bayubai.Identity.Persistence;

internal sealed class NoCurrentConsultant : ICurrentConsultant
{
    public Guid? ConsultantId => null;
}
