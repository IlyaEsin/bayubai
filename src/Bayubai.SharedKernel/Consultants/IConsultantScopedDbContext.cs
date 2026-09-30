namespace Bayubai.SharedKernel.Consultants;

public interface IConsultantScopedDbContext
{
    Guid? CurrentConsultantId { get; }
}
