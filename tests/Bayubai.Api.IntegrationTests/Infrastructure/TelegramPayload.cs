using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using NodaTime;

namespace Bayubai.Api.IntegrationTests.Infrastructure;

internal static class TelegramPayload
{
    // Mirrors what the Telegram Login Widget passes to its JavaScript callback.
    public static Dictionary<string, object> Create(string id, string firstName, Instant authDate, string botToken = ApiFactory.TelegramBotToken)
    {
        var fields = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["auth_date"] = authDate.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
            ["first_name"] = firstName,
            ["id"] = id,
        };
        var dataCheckString = string.Join('\n', fields.Select(field => $"{field.Key}={field.Value}"));
        var secret = SHA256.HashData(Encoding.UTF8.GetBytes(botToken));
        var hash = Convert.ToHexStringLower(HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes(dataCheckString)));

        return new Dictionary<string, object>
        {
            ["id"] = long.Parse(id, CultureInfo.InvariantCulture),
            ["first_name"] = firstName,
            ["auth_date"] = authDate.ToUnixTimeSeconds(),
            ["hash"] = hash,
        };
    }
}
