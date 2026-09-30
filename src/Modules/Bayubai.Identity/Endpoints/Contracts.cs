using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Bayubai.Identity.Accounts;
using Bayubai.SharedKernel.Localization;
using Bayubai.SharedKernel.Time;
using Bayubai.SharedKernel.Validation;
using NodaTime;

namespace Bayubai.Identity.Endpoints;

internal sealed record EmailStartRequest
{
    [Required, PlainEmail, StringLength(256)]
    public required string Email { get; init; }

    [Required, StringLength(2048)]
    public required string CallbackUrl { get; init; }

    [Required, AllowedValues(Languages.Russian, Languages.English)]
    public required string Language { get; init; }

    [Required, IanaTimeZone]
    public required string TimeZone { get; init; }

    [Required, AllowedValues(SignInModes.SignIn, SignInModes.Link)]
    public string Mode { get; init; } = SignInModes.SignIn;
}

internal sealed record EmailCompleteRequest
{
    [Required, StringLength(128)]
    public required string Token { get; init; }
}

internal sealed record MeResponse(
    Guid Id,
    string DisplayName,
    string Language,
    string TimeZone,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> SignInMethods);

internal sealed record TelegramCompleteRequest
{
    [Required]
    public required Dictionary<string, JsonElement> Auth { get; init; }

    [Required, AllowedValues(SignInModes.SignIn, SignInModes.Link)]
    public string Mode { get; init; } = SignInModes.SignIn;

    [Required, AllowedValues(Languages.Russian, Languages.English)]
    public string Language { get; init; } = Languages.Default;

    [Required, IanaTimeZone]
    public string TimeZone { get; init; } = TimeZones.Default;
}

internal sealed record ProvidersResponse(IReadOnlyList<string> Providers, string? TelegramBotName);

internal sealed record UpdateProfileRequest
{
    [Required, StringLength(100, MinimumLength = 1)]
    public required string DisplayName { get; init; }

    [Required, AllowedValues(Languages.Russian, Languages.English)]
    public required string Language { get; init; }

    [Required, IanaTimeZone]
    public required string TimeZone { get; init; }
}

internal sealed record CreateConsultantRequest
{
    [Required, PlainEmail, StringLength(256)]
    public required string Email { get; init; }

    [Required, StringLength(100, MinimumLength = 1)]
    public required string DisplayName { get; init; }

    // Parents see the consultant's local time, so a new consultant starts with a real zone instead of UTC.
    [Required, AllowedValues(Languages.Russian, Languages.English)]
    public required string Language { get; init; }

    [Required, IanaTimeZone]
    public required string TimeZone { get; init; }
}

internal sealed record CreateConsultantResponse(Guid UserId);

internal sealed record CreateInvitationResponse(Guid Id, string Url, Instant ExpiresAt);

internal sealed record InvitationResponse(Guid Id, Instant CreatedAt, Instant ExpiresAt, string Status);

internal sealed record AcceptInvitationRequest
{
    [Required, StringLength(128)]
    public required string Token { get; init; }
}

internal sealed record ConsultantResponse(Guid UserId, string DisplayName, string TimeZone, Instant LinkedAt);

// Language and TimeZone tell a consultant which language the client reads and what time it is for them.
internal sealed record ClientResponse(Guid UserId, string DisplayName, string Language, string TimeZone, Instant LinkedAt);
