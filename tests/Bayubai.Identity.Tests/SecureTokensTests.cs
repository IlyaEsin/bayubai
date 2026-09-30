using System.Security.Cryptography;
using System.Text;
using Bayubai.Identity.Security;

namespace Bayubai.Identity.Tests;

public class SecureTokensTests
{
    [Fact]
    public void Token_is_url_safe_and_hash_is_sha256_hex()
    {
        var token = SecureTokens.Create();

        token.Value.Length.ShouldBe(43);
        token.Value.ShouldAllBe(c => char.IsAsciiLetterOrDigit(c) || c == '-' || c == '_');
        token.Hash.ShouldBe(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token.Value))));
        SecureTokens.Hash(token.Value).ShouldBe(token.Hash);
    }

    [Fact]
    public void Tokens_are_unique() => SecureTokens.Create().Value.ShouldNotBe(SecureTokens.Create().Value);
}
