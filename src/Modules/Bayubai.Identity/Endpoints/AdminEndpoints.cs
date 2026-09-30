using Bayubai.Identity.Accounts;
using Bayubai.SharedKernel.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Bayubai.Identity.Endpoints;

internal static class AdminEndpoints
{
    public static void MapAdmin(this RouteGroupBuilder group)
    {
        group.MapPost("/admin/consultants", CreateConsultantAsync)
            .WithName("CreateConsultant")
            .RequireAuthorization(IdentityPolicies.Admin)
            .WithRequestValidation<CreateConsultantRequest>();
    }

    private static async Task<Ok<CreateConsultantResponse>> CreateConsultantAsync(
        CreateConsultantRequest request,
        AccountService accounts,
        CancellationToken cancellationToken)
    {
        var user = await accounts.EnsureConsultantAsync(
            request.Email,
            request.DisplayName.Trim(),
            new NewUserDefaults(request.Language, request.TimeZone),
            cancellationToken);
        return TypedResults.Ok(new CreateConsultantResponse(user.Id));
    }
}
