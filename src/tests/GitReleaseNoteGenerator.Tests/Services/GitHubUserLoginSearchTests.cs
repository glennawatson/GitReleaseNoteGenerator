// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net;

using GitReleaseNoteGenerator.Services;
using GitReleaseNoteGenerator.Tests.Fakes;

using Microsoft.Extensions.Logging.Abstractions;

namespace GitReleaseNoteGenerator.Tests.Services;

/// <summary>Tests for <see cref="GitHubUserLoginSearch"/>, driving the live search-users API path through a fake HTTP handler.</summary>
public class GitHubUserLoginSearchTests
{
    /// <summary>The stand-in access token handed to the client factory; the fake transport never inspects it.</summary>
    private const string Token = "token";

    /// <summary>Tests that a matching search result yields the user's login.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task FindLoginByEmailAsyncWithMatchReturnsLogin()
    {
        const string Json = """
            {"total_count":1,"incomplete_results":false,"items":[{"login":"octocat","id":1}]}
            """;
        var handler = new FakeHttpMessageHandler(static _ => (HttpStatusCode.OK, Json));
        var search = new GitHubUserLoginSearch(GitHubClientFactory.Create(Token, handler), NullLogger.Instance);

        var login = await search.FindLoginByEmailAsync("octocat@example.com");

        await Assert.That(login).IsEqualTo("octocat");
    }

    /// <summary>Tests that an empty search result yields null.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task FindLoginByEmailAsyncWithNoMatchReturnsNull()
    {
        const string Json = """
            {"total_count":0,"incomplete_results":false,"items":[]}
            """;
        var handler = new FakeHttpMessageHandler(static _ => (HttpStatusCode.OK, Json));
        var search = new GitHubUserLoginSearch(GitHubClientFactory.Create(Token, handler), NullLogger.Instance);

        var login = await search.FindLoginByEmailAsync("nobody@example.com");

        await Assert.That(login).IsNull();
    }
}
