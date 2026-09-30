using Bayubai.Identity.Email;

namespace Bayubai.Identity.Tests;

public class EmailTests
{
    private const string Link = "https://app.example.test/auth/email?token=abc";

    [Fact]
    public void Russian_email_contains_the_link_and_russian_text()
    {
        var message = MagicLinkEmail.Compose("anna@example.test", "ru", Link);

        message.To.ShouldBe("anna@example.test");
        message.Subject.ShouldBe("Вход в Баюбай");
        message.TextBody.ShouldContain(Link);
    }

    [Fact]
    public void English_email_is_the_fallback()
    {
        var message = MagicLinkEmail.Compose("anna@example.test", "en", Link);

        message.Subject.ShouldBe("Sign in to Bayubai");
        message.TextBody.ShouldContain(Link);
    }

    [Fact]
    public void Aspire_connection_string_sets_host_and_port()
    {
        var options = new EmailOptions();

        options.ApplyConnectionString("smtp://localhost:41025");

        options.Host.ShouldBe("localhost");
        options.Port.ShouldBe(41025);
    }

    [Fact]
    public void Aspire_endpoint_connection_string_sets_host_and_port()
    {
        var options = new EmailOptions();

        options.ApplyConnectionString("Endpoint=smtp://localhost:41025");

        options.Host.ShouldBe("localhost");
        options.Port.ShouldBe(41025);
    }

    [Fact]
    public void Missing_connection_string_keeps_configured_values()
    {
        var options = new EmailOptions { Host = "smtp.azurecomm.net", Port = 587 };

        options.ApplyConnectionString(null);

        options.Host.ShouldBe("smtp.azurecomm.net");
        options.Port.ShouldBe(587);
    }
}
