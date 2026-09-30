using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Bayubai.SharedKernel.Errors;
using Microsoft.AspNetCore.Http;

namespace Bayubai.SharedKernel.Validation;

public sealed class ValidationFilter<TRequest> : IEndpointFilter where TRequest : class
{
    // Field-level values stay codes too, so clients translate them like any other error.
    public const string InvalidValue = "invalid";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var request = context.Arguments.OfType<TRequest>().FirstOrDefault();
        if (request is null)
        {
            return await next(context);
        }

        var results = new List<ValidationResult>();
        if (Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true))
        {
            return await next(context);
        }

        var errors = results
            .SelectMany(result => result.MemberNames)
            .Select(member => JsonNamingPolicy.CamelCase.ConvertName(member))
            .Distinct()
            .ToDictionary(member => member, _ => new[] { InvalidValue });

        return TypedResults.ValidationProblem(
            errors,
            extensions: new Dictionary<string, object?> { [ApiProblems.CodeKey] = CommonErrors.ValidationFailed });
    }
}
