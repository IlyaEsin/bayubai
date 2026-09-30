namespace CareNest.SharedKernel.Errors;

public static class CommonErrors
{
    public const string ValidationFailed = "validation_failed";

    public const string RateLimited = "rate_limited";

    public static ApiError TooManyRequests { get; } = new(RateLimited, 429);

    public static IReadOnlyList<string> Codes { get; } = [ValidationFailed, RateLimited];
}
