// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Octokit;

namespace GitReleaseNoteGenerator.Services;

/// <summary>
/// Creates authenticated GitHub API clients.
/// </summary>
public static class GitHubClientFactory
{
    /// <summary>
    /// Creates a new <see cref="GitHubClient"/> authenticated with the given token.
    /// </summary>
    /// <param name="token">The GitHub personal access token.</param>
    /// <returns>An authenticated GitHub client.</returns>
    public static GitHubClient Create(string token) =>
        new(new ProductHeaderValue("GitReleaseNoteGenerator"))
        {
            Credentials = new Credentials(token),
        };
}
