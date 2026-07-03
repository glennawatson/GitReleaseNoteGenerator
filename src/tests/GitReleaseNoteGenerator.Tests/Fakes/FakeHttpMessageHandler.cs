// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net;
using System.Text;

namespace GitReleaseNoteGenerator.Tests.Fakes;

/// <summary>
/// A test <see cref="HttpMessageHandler"/> that returns canned JSON responses based on the
/// incoming request, used to drive the Refit-backed API layer without real network calls.
/// </summary>
internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    /// <summary>Produces the (status, JSON body) to return for a given request.</summary>
    private readonly Func<HttpRequestMessage, (HttpStatusCode Status, string Json)> _responder;

    /// <summary>Initializes a new instance of the <see cref="FakeHttpMessageHandler"/> class.</summary>
    /// <param name="responder">Maps a request to the response status and JSON body to return.</param>
    public FakeHttpMessageHandler(Func<HttpRequestMessage, (HttpStatusCode Status, string Json)> responder) =>
        _responder = responder;

    /// <summary>Gets the requests that have been handled, in order.</summary>
    public List<HttpRequestMessage> Requests { get; } = [];

    /// <inheritdoc/>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        var (status, json) = _responder(request);

        var response = new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
            RequestMessage = request,
        };

        return Task.FromResult(response);
    }
}
