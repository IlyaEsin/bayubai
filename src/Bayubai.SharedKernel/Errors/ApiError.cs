namespace Bayubai.SharedKernel.Errors;

// Code is the contract clients translate; the API never sends human-readable text.
public sealed record ApiError(string Code, int Status);
