using Bayubai.SharedKernel.Consultants;
using Microsoft.AspNetCore.Http;

namespace Bayubai.Identity.Security;

internal sealed class HttpCurrentConsultant(IHttpContextAccessor accessor) : ICurrentConsultant
{
    public Guid? ConsultantId
    {
        get
        {
            var principal = accessor.HttpContext?.User;
            return principal is not null && principal.IsInRole(IdentityRoles.Consultant) ? principal.GetUserId() : null;
        }
    }
}
