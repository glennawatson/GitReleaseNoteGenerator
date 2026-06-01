// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net;

using GitReleaseNoteGenerator.Services;
using GitReleaseNoteGenerator.Tests.Fakes;

using Microsoft.Extensions.Logging.Abstractions;

namespace GitReleaseNoteGenerator.Tests.Services;

/// <summary>
/// Tests for <see cref="GitHubUserLoginSearch"/>, driving the live search-users API path
/// through a fake HTTP handler.
/// </summary>
public class GitHubUserLoginSearchTests
{
    /// <summary>
    /// Tests that a matching search result yields the user's login.
    /// </summary>
    [Test]
    public async Task FindLoginByEmailAsync_WithMatch_ReturnsLogin()
    {
        const string Json = """
            {"total_count":1,"incomplete_results":false,"items":[{"login":"glennawatson","id":1}]}
            """;
        var handler = new FakeHttpMessageHandler(_ => (HttpStatusCode.OK, Json));
        var search = new GitHubUserLoginSearch(GitHubClientFactory.Create("token", handler), NullLogger.Instance);

        var login = await search.FindLoginByEmailAsync("glenn@glennwatson.net");

        await Assert.That(login).IsEqualTo("glennawatson");
    }

    /// <summary>
    /// Tests that an empty search result yields null.
    /// </summary>
    [Test]
    public async Task FindLoginByEmailAsync_WithNoMatch_ReturnsNull()
    {
        const string Json = """
            {"total_count":0,"incomplete_results":false,"items":[]}
            """;
        var handler = new FakeHttpMessageHandler(_ => (HttpStatusCode.OK, Json));
        var search = new GitHubUserLoginSearch(GitHubClientFactory.Create("token", handler), NullLogger.Instance);

        var login = await search.FindLoginByEmailAsync("nobody@example.com");

        await Assert.That(login).IsNull();
    }
}
