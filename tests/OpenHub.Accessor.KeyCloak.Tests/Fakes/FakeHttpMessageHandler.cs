using System.Net.Http.Headers;

namespace OpenHub.Accessor.KeyCloak.Tests.Fakes;

/// <summary>
/// A route-based fake <see cref="HttpMessageHandler"/>. Register one responder
/// per method+path (optionally gated on a Task for concurrency tests); every
/// invocation is captured for later assertions (call counts, headers, body).
/// </summary>
public sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly List<(Func<HttpRequestMessage, bool> Match, Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Respond)> _routes = [];

    public List<CapturedRequest> Requests { get; } = [];

    public FakeHttpMessageHandler When(HttpMethod method, string pathContains, Func<HttpRequestMessage, HttpResponseMessage> respond)
        => WhenAsync(method, pathContains, (req, _) => Task.FromResult(respond(req)));

    public FakeHttpMessageHandler WhenAsync(
        HttpMethod method,
        string pathContains,
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
    {
        _routes.Add((
            req => req.Method == method && (req.RequestUri?.AbsolutePath.Contains(pathContains, StringComparison.Ordinal) ?? false),
            respond));
        return this;
    }

    public int RequestCountTo(string pathContains)
        => Requests.Count(r => r.RequestUri?.AbsolutePath.Contains(pathContains, StringComparison.Ordinal) ?? false);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Snapshot method/path/headers/body now — the real HttpClient pipeline
        // may dispose the request's content shortly after SendAsync returns.
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add(new CapturedRequest(request.Method, request.RequestUri, request.Headers.Authorization, body));

        foreach (var (match, respond) in _routes)
        {
            if (match(request))
            {
                return await respond(request, cancellationToken);
            }
        }

        throw new InvalidOperationException($"No fake route registered for {request.Method} {request.RequestUri}");
    }
}

public sealed record CapturedRequest(HttpMethod Method, Uri? RequestUri, AuthenticationHeaderValue? Authorization, string? Body);
