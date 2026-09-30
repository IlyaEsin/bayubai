namespace Bayubai.Identity.Email;

internal interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
