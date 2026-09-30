using MimeKit;

namespace Bayubai.Identity.Accounts;

// Email is stored as an external login (provider "email") so it never becomes a merge key for other providers.
internal static class EmailLogin
{
    public const string Provider = "email";

    public static string Normalize(string email) => email.Trim().ToLowerInvariant();

    // A mailbox like "Anna <anna@example.test>" would key one account while MimeKit mails the bare address, creating a second one; only the plain address is accepted as an account key.
    public static bool TryNormalize(string? email, out string normalized)
    {
        normalized = "";
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        var trimmed = email.Trim();
        if (!trimmed.Contains('@') ||
            !MailboxAddress.TryParse(trimmed, out var mailbox) ||
            !string.IsNullOrEmpty(mailbox.Name) ||
            !string.Equals(mailbox.Address, trimmed, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        normalized = trimmed.ToLowerInvariant();
        return true;
    }

    public static string DisplayNameFrom(string normalizedEmail)
    {
        var at = normalizedEmail.IndexOf('@');
        return at > 0 ? normalizedEmail[..at] : normalizedEmail;
    }
}
