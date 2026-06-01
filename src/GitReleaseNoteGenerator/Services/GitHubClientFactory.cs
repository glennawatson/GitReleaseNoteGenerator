// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Octokit;
using Octokit.Internal;

namespace GitReleaseNoteGenerator.Services;

/// <summary>
/// Creates authenticated GitHub API clients.
/// </summary>
public static class GitHubClientFactory
{
    /// <summary>
    /// The product header sent with GitHub API requests.
    /// </summary>
    private const string ProductName = "GitReleaseNoteGenerator";

    /// <summary>
    /// Creates a new <see cref="GitHubClient"/> authenticated with the given token.
    /// </summary>
    /// <param name="token">The GitHub personal access token.</param>
    /// <returns>An authenticated GitHub client.</returns>
    public static GitHubClient Create(string token) =>
        new(new ProductHeaderValue(ProductName))
        {
            Credentials = new(token)
        };

    /// <summary>
    /// Creates a <see cref="GitHubClient"/> whose transport is backed by the supplied
    /// <see cref="HttpMessageHandler"/>. This is the HTTP seam used to drive the API layer
    /// from tests with canned responses (and is equally usable for custom proxies/transports).
    /// </summary>
    /// <param name="token">The GitHub personal access token.</param>
    /// <param name="handler">The HTTP message handler that services API requests.</param>
    /// <returns>An authenticated GitHub client using the supplied handler.</returns>
    public static GitHubClient Create(string token, HttpMessageHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var connection = new Connection(
            new ProductHeaderValue(ProductName),
            new HttpClientAdapter(() => handler));

        return new GitHubClient(connection)
        {
            Credentials = new(token),
        };
    }
}
