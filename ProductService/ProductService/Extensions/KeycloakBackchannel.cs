namespace ProductService.API.Extensions;

public static class KeycloakBackchannel
{
    public static Uri Rewrite(Uri requestUri, string publicAuthority, string? internalAuthority)
    {
        if (string.IsNullOrWhiteSpace(internalAuthority))
        {
            return requestUri;
        }

        var publicPrefix = publicAuthority.TrimEnd('/');
        var internalPrefix = internalAuthority.TrimEnd('/');
        var url = requestUri.AbsoluteUri;

        if (!url.StartsWith(publicPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return requestUri;
        }

        return new Uri(internalPrefix + url[publicPrefix.Length..]);
    }
}

public sealed class KeycloakBackchannelHandler : DelegatingHandler
{
    private readonly string _publicAuthority;
    private readonly string _internalAuthority;

    public KeycloakBackchannelHandler(string publicAuthority, string internalAuthority)
        : base(new HttpClientHandler())
    {
        _publicAuthority = publicAuthority;
        _internalAuthority = internalAuthority;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri is not null)
        {
            request.RequestUri = KeycloakBackchannel.Rewrite(request.RequestUri, _publicAuthority, _internalAuthority);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
