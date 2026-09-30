namespace Bayubai.SharedKernel.Web;

public sealed class FrontendOptions
{
    public const string Section = "Frontend";

    // Browser origins of the web apps; used for CORS and for validating return and callback URLs.
    public string[] Origins { get; set; } = [];

    public string ClientAppUrl { get; set; } = "";

    // An empty or relative value would silently break CORS and every emailed link, so the host refuses to start instead.
    public bool IsValid() =>
        Origins.Length > 0 && Origins.All(IsHttpUrl) && IsHttpUrl(ClientAppUrl);

    private static bool IsHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
}
