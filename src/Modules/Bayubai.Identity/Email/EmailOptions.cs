using System.Data.Common;

namespace Bayubai.Identity.Email;

internal sealed class EmailOptions
{
    public const string Section = "Email";
    public const string ConnectionStringName = "email";

    public string From { get; set; } = "Баюбай <no-reply@bayubai.local>";

    public string Host { get; set; } = "localhost";

    public int Port { get; set; } = 1025;

    public bool UseStartTls { get; set; }

    public string? UserName { get; set; }

    public string? Password { get; set; }

    // Aspire's Mailpit resource provides "Endpoint=smtp://host:port" (a key-value connection string); a bare "smtp://host:port" URI is also accepted; production configures the Email section instead.
    public void ApplyConnectionString(string? connectionString)
    {
        if (Uri.TryCreate(connectionString, UriKind.Absolute, out var uri))
        {
            Host = uri.Host;
            Port = uri.Port;
            return;
        }

        if (connectionString is null)
        {
            return;
        }

        try
        {
            var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
            if (builder.TryGetValue("Endpoint", out var endpoint)
                && Uri.TryCreate(endpoint as string, UriKind.Absolute, out var endpointUri))
            {
                Host = endpointUri.Host;
                Port = endpointUri.Port;
            }
        }
        catch (ArgumentException)
        {
            // Unparseable connection string: keep the configured values.
        }
    }
}
