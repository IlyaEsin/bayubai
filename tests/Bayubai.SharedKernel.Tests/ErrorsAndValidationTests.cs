using System.ComponentModel.DataAnnotations;
using Bayubai.SharedKernel.Errors;
using Bayubai.SharedKernel.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Bayubai.SharedKernel.Tests;

public class ErrorsAndValidationTests
{
    public sealed record SampleRequest
    {
        [Required, StringLength(3)]
        public string? DisplayName { get; init; }
    }

    [Fact]
    public void Api_error_becomes_problem_with_status_and_code()
    {
        var problem = new ApiError("identity.invite_used", 409).ToProblem();

        problem.StatusCode.ShouldBe(409);
        problem.ProblemDetails.Extensions[ApiProblems.CodeKey].ShouldBe("identity.invite_used");
    }

    [Fact]
    public async Task Invalid_request_short_circuits_with_coded_validation_problem()
    {
        var filter = new ValidationFilter<SampleRequest>();
        var context = new DefaultEndpointFilterInvocationContext(new DefaultHttpContext(), new SampleRequest { DisplayName = "too long" });
        var nextCalled = false;

        var result = await filter.InvokeAsync(context, _ => { nextCalled = true; return ValueTask.FromResult<object?>(null); });

        nextCalled.ShouldBeFalse();
        var problem = result.ShouldBeOfType<ValidationProblem>();
        problem.ProblemDetails.Errors.Keys.ShouldBe(new[] { "displayName" });
        problem.ProblemDetails.Errors["displayName"].ShouldBe(new[] { ValidationFilter<SampleRequest>.InvalidValue });
        problem.ProblemDetails.Extensions[ApiProblems.CodeKey].ShouldBe(CommonErrors.ValidationFailed);
    }

    [Fact]
    public async Task Valid_request_reaches_the_handler()
    {
        var filter = new ValidationFilter<SampleRequest>();
        var context = new DefaultEndpointFilterInvocationContext(new DefaultHttpContext(), new SampleRequest { DisplayName = "Ann" });

        var result = await filter.InvokeAsync(context, _ => ValueTask.FromResult<object?>("handled"));

        result.ShouldBe("handled");
    }
}
