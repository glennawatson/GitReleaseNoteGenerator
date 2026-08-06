// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net;

using GitReleaseNoteGenerator.Services;
using GitReleaseNoteGenerator.Tests.Fakes;

namespace GitReleaseNoteGenerator.Tests.Services;

/// <summary>Tests for <see cref="GitHubClientFactory"/>.</summary>
public class GitHubClientFactoryTests
{
    /// <summary>A minimal repository payload.</summary>
    private const string RepoJson = """{ "default_branch": "main" }""";

    /// <summary>The stand-in access token used when only client construction is under test.</summary>
    private const string ExampleToken = "ghp_example_token";

    /// <summary>Tests that a client is created for the supplied token.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task CreateWithTokenReturnsClient()
    {
        var client = GitHubClientFactory.Create(ExampleToken);

        await Assert.That(client).IsNotNull();
    }

    /// <summary>Tests that requests carry the bearer authorization and product User-Agent headers.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task CreateWithHandlerSendsAuthorizationAndUserAgent()
    {
        const string token = "ghp_token";
        var handler = new FakeHttpMessageHandler(static _ => (HttpStatusCode.OK, RepoJson));
        var client = GitHubClientFactory.Create(token, handler);

        await client.GetRepositoryAsync("owner", "repo");

        await Assert.That(handler.Requests.Count).IsEqualTo(1);

        var request = handler.Requests[0];
        await Assert.That(request.Headers.Authorization!.Scheme).IsEqualTo("Bearer");
        await Assert.That(request.Headers.Authorization!.Parameter).IsEqualTo(token);
        await Assert.That(request.Headers.UserAgent.ToString()).Contains("GitReleaseNoteGenerator");
    }
}
