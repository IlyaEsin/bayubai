namespace Bayubai.Identity;

internal sealed class IdentityModuleOptions
{
    public const string Section = "Identity";

    // Users signing in with one of these emails receive the admin role.
    public string[] AdminEmails { get; set; } = [];

    // Shared parent domain of app., studio. and api. in production; null locally.
    public string? CookieDomain { get; set; }

    public int InvitationLifetimeDays { get; set; } = 14;

    // Sign-in emails one network address may request per throttle window, on top of the per-email limit.
    public int EmailStartsPerAddressWindow { get; set; } = 20;

    public string? TelegramBotToken { get; set; }

    public string? TelegramBotName { get; set; }
}
