namespace Bayubai.Identity.Accounts;

internal static class DisplayNames
{
    public const int MaxLength = 100;

    public static string Normalize(string? name)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        return trimmed.Length <= MaxLength ? trimmed : trimmed[..MaxLength];
    }
}
