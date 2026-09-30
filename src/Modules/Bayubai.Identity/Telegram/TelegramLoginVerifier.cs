using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using NodaTime;

namespace Bayubai.Identity.Telegram;

internal sealed record TelegramLogin(string Id, string? DisplayName);

internal static class TelegramLoginVerifier
{
    public static readonly Duration MaxAge = Duration.FromMinutes(10);
    private static readonly Duration ClockSkew = Duration.FromMinutes(1);

    // https://core.telegram.org/widgets/login#checking-authorization
    public static TelegramLogin? Verify(IReadOnlyDictionary<string, string> fields, string botToken, Instant now)
    {
        if (!fields.TryGetValue("hash", out var hashHex)
            || !fields.TryGetValue("id", out var id)
            || !fields.TryGetValue("auth_date", out var authDateText)
            || !long.TryParse(authDateText, NumberStyles.None, CultureInfo.InvariantCulture, out var authDateSeconds))
        {
            return null;
        }

        byte[] actual;
        try
        {
            actual = Convert.FromHexString(hashHex);
        }
        catch (FormatException)
        {
            return null;
        }

        var dataCheckString = string.Join('\n', fields
            .Where(field => field.Key != "hash")
            .OrderBy(field => field.Key, StringComparer.Ordinal)
            .Select(field => $"{field.Key}={field.Value}"));
        var secret = SHA256.HashData(Encoding.UTF8.GetBytes(botToken));
        var expected = HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes(dataCheckString));
        if (!CryptographicOperations.FixedTimeEquals(expected, actual))
        {
            return null;
        }

        var age = now - Instant.FromUnixTimeSeconds(authDateSeconds);
        if (age > MaxAge || age < -ClockSkew)
        {
            return null;
        }

        var fullName = string.Join(' ', new[] { fields.GetValueOrDefault("first_name"), fields.GetValueOrDefault("last_name") }
            .Where(part => !string.IsNullOrWhiteSpace(part)));
        return new TelegramLogin(id, fullName.Length > 0 ? fullName : fields.GetValueOrDefault("username"));
    }
}
