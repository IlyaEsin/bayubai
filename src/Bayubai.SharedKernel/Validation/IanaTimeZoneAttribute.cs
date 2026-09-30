using System.ComponentModel.DataAnnotations;
using Bayubai.SharedKernel.Time;

namespace Bayubai.SharedKernel.Validation;

[AttributeUsage(AttributeTargets.Property)]
public sealed class IanaTimeZoneAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) => value is null || (value is string id && TimeZones.IsValid(id));
}
