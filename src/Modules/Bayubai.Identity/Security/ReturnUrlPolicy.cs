using System.Diagnostics.CodeAnalysis;
using Bayubai.SharedKernel.Web;
using Microsoft.Extensions.Options;

namespace Bayubai.Identity.Security;

internal sealed class ReturnUrlPolicy(IOptions<FrontendOptions> frontend)
{
    private readonly string[] _origins = frontend.Value.Origins
        .Select(origin => new Uri(origin).GetLeftPart(UriPartial.Authority))
        .ToArray();

    public bool IsAllowed([NotNullWhen(true)] string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
            || uri.UserInfo.Length > 0)
        {
            return false;
        }

        var origin = uri.GetLeftPart(UriPartial.Authority);
        return _origins.Any(allowed => string.Equals(allowed, origin, StringComparison.OrdinalIgnoreCase));
    }
}
