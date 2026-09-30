using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Bayubai.Identity.Email;

namespace Bayubai.Api.IntegrationTests.Infrastructure;

internal sealed partial class FakeEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<EmailMessage> _sent = new();

    public IReadOnlyList<EmailMessage> SentTo(string address) => _sent.Where(message => message.To == address).ToList();

    public string LatestTokenFor(string address) => TokenPattern().Match(SentTo(address)[^1].TextBody).Groups[1].Value;

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        _sent.Enqueue(message);
        return Task.CompletedTask;
    }

    [GeneratedRegex("token=([A-Za-z0-9_-]+)")]
    private static partial Regex TokenPattern();
}
