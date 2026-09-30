using Bayubai.SharedKernel.Localization;
using Bayubai.SharedKernel.Time;
using Bayubai.SharedKernel.Validation;

namespace Bayubai.SharedKernel.Tests;

public class LanguagesAndTimeZonesTests
{
    [Theory]
    [InlineData("ru", true)]
    [InlineData("en", true)]
    [InlineData("RU", false)]
    [InlineData("de", false)]
    [InlineData(null, false)]
    public void Language_support(string? value, bool expected) => Languages.IsSupported(value).ShouldBe(expected);

    [Fact]
    public void Unsupported_language_falls_back_to_default() => Languages.OrDefault("de").ShouldBe(Languages.English);

    [Theory]
    [InlineData("Europe/Moscow", true)]
    [InlineData("UTC", true)]
    [InlineData("Mars/Olympus", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Time_zone_validity(string? value, bool expected) => TimeZones.IsValid(value).ShouldBe(expected);

    [Fact]
    public void Invalid_time_zone_falls_back_to_utc() => TimeZones.OrDefault("Mars/Olympus").ShouldBe("UTC");

    [Fact]
    public void Iana_attribute_accepts_null_and_known_zones_only()
    {
        var attribute = new IanaTimeZoneAttribute();
        attribute.IsValid(null).ShouldBeTrue();
        attribute.IsValid("Asia/Yekaterinburg").ShouldBeTrue();
        attribute.IsValid("Moscow").ShouldBeFalse();
        attribute.IsValid(42).ShouldBeFalse();
    }
}
