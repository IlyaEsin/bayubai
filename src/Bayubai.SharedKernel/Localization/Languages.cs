using System.Diagnostics.CodeAnalysis;

namespace Bayubai.SharedKernel.Localization;

public static class Languages
{
    public const string Russian = "ru";
    public const string English = "en";
    public const string Default = English;

    public static IReadOnlyList<string> All { get; } = [Russian, English];

    public static bool IsSupported([NotNullWhen(true)] string? value) => value is not null && All.Contains(value);

    public static string OrDefault(string? value) => IsSupported(value) ? value : Default;
}
