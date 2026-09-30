using System.ComponentModel.DataAnnotations;

namespace Bayubai.Identity.Accounts;

// EmailAddressAttribute accepts "Name <addr>"; the account key must be the bare address only.
[AttributeUsage(AttributeTargets.Property)]
internal sealed class PlainEmailAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) => value is null || (value is string email && EmailLogin.TryNormalize(email, out _));
}
