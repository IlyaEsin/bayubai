namespace Bayubai.Identity.Email;

internal sealed record EmailMessage(string To, string Subject, string TextBody);
