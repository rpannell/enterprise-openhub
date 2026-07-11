using System.Net.Http.Headers;

namespace OpenHub.Accessor.RealtimeMessaging.Tests.Fakes;

/// <summary>
/// A route-based fake <see cref="HttpMessageHandler"/>. Register one responder per
/// method+path; every invocation is captured for later assertions (headers, body).
/// Mirrors OpenHub.Accessor.KeyCloak.Tests' fake of the same name.
/// </summary>
public sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly List<(Func<HttpRequestMessage, bool> Match, Func<HttpRequestMessage, HttpResponseMessage> Respond)> _routes = [];

    public List<CapturedRequest> Requests { get; } = [];

    public FakeHttpMessageHandler When(HttpMethod method, string pathContains, Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        _routes.Add((
            req => req.Method == method && (req.RequestUri?.AbsolutePath.Contains(pathContains, StringComparison.Ordinal) ?? false),
            respond));
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add(new CapturedRequest(request.Method, request.RequestUri, request.Headers.Authorization, body));

        foreach (var (match, respond) in _routes)
        {
            if (match(request))
                return respond(request);
        }

        throw new InvalidOperationException($"No fake route registered for {request.Method} {request.RequestUri}");
    }
}

public sealed record CapturedRequest(HttpMethod Method, Uri? RequestUri, AuthenticationHeaderValue? Authorization, string? Body);
