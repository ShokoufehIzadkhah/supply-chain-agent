using System.Net;

namespace Agent.Tools.Tests;

/// <summary>
/// Minimal <see cref="HttpMessageHandler"/> stub that mocks the ERP API: it records every request it
/// receives and answers with whatever the supplied responder returns.
/// </summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

    public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        => _responder = responder;

    /// <summary>Requests seen by the handler, in call order.</summary>
    public List<HttpRequestMessage> Requests { get; } = [];

    /// <summary>
    /// Body of the most recent request, read before the request is disposed. <c>null</c> when the
    /// request had no content.
    /// </summary>
    public string? LastRequestBody { get; private set; }

    /// <summary>Value of the <c>X-Api-Key</c> header on the most recent request, or <c>null</c> when absent.</summary>
    public string? LastRequestApiKey { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Requests.Add(request);

        LastRequestBody = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);

        LastRequestApiKey = request.Headers.TryGetValues("X-Api-Key", out var values)
            ? values.First()
            : null;

        return _responder(request);
    }
}
