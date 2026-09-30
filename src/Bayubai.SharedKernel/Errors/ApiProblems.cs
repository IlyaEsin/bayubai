using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Bayubai.SharedKernel.Errors;

public static class ApiProblems
{
    public const string CodeKey = "code";

    public static ProblemHttpResult ToProblem(this ApiError error) =>
        TypedResults.Problem(
            statusCode: error.Status,
            extensions: new Dictionary<string, object?> { [CodeKey] = error.Code });
}
