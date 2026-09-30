using System.Security.Cryptography;
using System.Text;
using Bayubai.Identity.Telegram;
using NodaTime;

namespace Bayubai.Identity.Tests;

public class TelegramLoginVerifierTests
{
    private const string BotToken = "123456:TEST-TOKEN";
    private static readonly Instant Now = Instant.FromUtc(2026, 1, 5, 9, 0);

    [Fact]
    public void Valid_payload_returns_login_with_full_name()
    {
        var login = TelegramLoginVerifier.Verify(Signed(Payload(Now)), BotToken, Now);

        login.ShouldNotBeNull();
        login.Id.ShouldBe("987654321");
        login.DisplayName.ShouldBe("Anna Petrova");
    }

    [Fact]
    public void Unknown_future_fields_are_part_of_the_signature()
    {
        var fields = Payload(Now);
        fields["allows_write_to_pm"] = "true";

        TelegramLoginVerifier.Verify(Signed(fields), BotToken, Now).ShouldNotBeNull();
    }

    [Fact]
    public void Username_is_used_when_there_is_no_name()
    {
        var fields = Payload(Now);
        fields.Remove("first_name");
        fields.Remove("last_name");

        TelegramLoginVerifier.Verify(Signed(fields), BotToken, Now)!.DisplayName.ShouldBe("anna_p");
    }

    [Fact]
    public void Tampered_field_is_rejected()
    {
        var fields = Signed(Payload(Now));
        fields["id"] = "1";

        TelegramLoginVerifier.Verify(fields, BotToken, Now).ShouldBeNull();
    }

    [Fact]
    public void Other_bot_token_is_rejected() =>
        TelegramLoginVerifier.Verify(Signed(Payload(Now)), "999:OTHER", Now).ShouldBeNull();

    [Fact]
    public void Missing_or_malformed_hash_is_rejected()
    {
        var withoutHash = Payload(Now);
        var malformed = Payload(Now);
        malformed["hash"] = "not-hex";

        TelegramLoginVerifier.Verify(withoutHash, BotToken, Now).ShouldBeNull();
        TelegramLoginVerifier.Verify(malformed, BotToken, Now).ShouldBeNull();
    }

    [Fact]
    public void Old_or_future_auth_date_is_rejected()
    {
        TelegramLoginVerifier.Verify(Signed(Payload(Now - Duration.FromMinutes(11))), BotToken, Now).ShouldBeNull();
        TelegramLoginVerifier.Verify(Signed(Payload(Now + Duration.FromMinutes(2))), BotToken, Now).ShouldBeNull();
    }

    private static Dictionary<string, string> Payload(Instant authDate) => new()
    {
        ["id"] = "987654321",
        ["first_name"] = "Anna",
        ["last_name"] = "Petrova",
        ["username"] = "anna_p",
        ["auth_date"] = authDate.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture),
    };

    // Independent implementation of https://core.telegram.org/widgets/login#checking-authorization
    private static Dictionary<string, string> Signed(Dictionary<string, string> fields)
    {
        var dataCheckString = string.Join('\n', fields.OrderBy(f => f.Key, StringComparer.Ordinal).Select(f => $"{f.Key}={f.Value}"));
        var secret = SHA256.HashData(Encoding.UTF8.GetBytes(BotToken));
        var hash = Convert.ToHexStringLower(HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes(dataCheckString)));
        return new Dictionary<string, string>(fields) { ["hash"] = hash };
    }
}
