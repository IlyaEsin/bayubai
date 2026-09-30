using Bayubai.Identity.Accounts;

namespace Bayubai.Identity.Tests;

public class EmailLoginTests
{
    [Fact]
    public void Normalize_trims_and_lowercases() => EmailLogin.Normalize("  Anna.P@Example.TEST ").ShouldBe("anna.p@example.test");

    [Fact]
    public void Display_name_is_the_local_part() => EmailLogin.DisplayNameFrom("anna.p@example.test").ShouldBe("anna.p");

    [Fact]
    public void TryNormalize_accepts_a_plain_address()
    {
        EmailLogin.TryNormalize("  Anna.P@Example.TEST ", out var normalized).ShouldBeTrue();
        normalized.ShouldBe("anna.p@example.test");
    }

    [Theory]
    [InlineData("Anna <anna@example.test>")]
    [InlineData("not-an-email")]
    [InlineData(null)]
    [InlineData("")]
    public void TryNormalize_rejects_anything_but_a_plain_address(string? email) =>
        EmailLogin.TryNormalize(email, out _).ShouldBeFalse();

    [Fact]
    public void Display_names_are_trimmed_and_capped()
    {
        DisplayNames.Normalize("  Anna  ").ShouldBe("Anna");
        DisplayNames.Normalize(null).ShouldBe("");
        DisplayNames.Normalize(new string('a', 150)).Length.ShouldBe(DisplayNames.MaxLength);
    }
}
