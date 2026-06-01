// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using GitReleaseNoteGenerator.Services;

using Octokit;

namespace GitReleaseNoteGenerator.Tests.Services;

/// <summary>
/// Tests for <see cref="GitHubClientFactory"/>.
/// </summary>
public class GitHubClientFactoryTests
{
    /// <summary>
    /// Tests that a client is created and authenticated with the supplied token.
    /// </summary>
    [Test]
    public async Task Create_WithToken_ReturnsOauthAuthenticatedClient()
    {
        var client = GitHubClientFactory.Create("ghp_example_token");

        await Assert.That(client).IsNotNull();
        await Assert.That(client.Credentials.AuthenticationType).IsEqualTo(AuthenticationType.Oauth);
    }
}
