// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net;

using GitReleaseNoteGenerator.Services;
using GitReleaseNoteGenerator.Tests.Fakes;

namespace GitReleaseNoteGenerator.Tests.Services;

/// <summary>
/// Tests for <see cref="GitHubClientFactory"/>.
/// </summary>
public class GitHubClientFactoryTests
{
    /// <summary>
    /// A minimal repository payload.
    /// </summary>
    private const string RepoJson = """{ "default_branch": "main" }""";

    /// <summary>
    /// Tests that a client is created for the supplied token.
    /// </summary>
    [Test]
    public async Task Create_WithToken_ReturnsClient()
    {
        var client = GitHubClientFactory.Create("ghp_example_token");

        await Assert.That(client).IsNotNull();
    }

    /// <summary>
    /// Tests that requests carry the bearer authorization and product User-Agent headers.
    /// </summary>
    [Test]
    public async Task Create_WithHandler_SendsAuthorizationAndUserAgent()
    {
        const string token = "ghp_token";
        var handler = new FakeHttpMessageHandler(_ => (HttpStatusCode.OK, RepoJson));
        var client = GitHubClientFactory.Create(token, handler);

        await client.GetRepositoryAsync("owner", "repo");

        var request = handler.Requests.Single();
        await Assert.That(request.Headers.Authorization!.Scheme).IsEqualTo("Bearer");
        await Assert.That(request.Headers.Authorization!.Parameter).IsEqualTo(token);
        await Assert.That(request.Headers.UserAgent.ToString()).Contains("GitReleaseNoteGenerator");
    }
}
