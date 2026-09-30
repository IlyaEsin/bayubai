using System.Diagnostics.CodeAnalysis;
using NodaTime;

namespace Bayubai.SharedKernel.Time;

public static class TimeZones
{
    public const string Default = "UTC";

    public static bool IsValid([NotNullWhen(true)] string? id) =>
        !string.IsNullOrEmpty(id) && DateTimeZoneProviders.Tzdb.GetZoneOrNull(id) is not null;

    public static string OrDefault(string? id) => IsValid(id) ? id : Default;
}
