using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Bayubai.Identity.Security;

internal sealed record SecureToken(string Value, string Hash);

internal static class SecureTokens
{
    // Only the hash is stored, so a database leak does not expose usable links.
    public static SecureToken Create()
    {
        var value = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        return new SecureToken(value, Hash(value));
    }

    public static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
