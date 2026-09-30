namespace Bayubai.SharedKernel.Consultants;

public interface ICurrentConsultant
{
    // Null when the caller is not a consultant; consultant-owned queries then return nothing.
    Guid? ConsultantId { get; }
}
