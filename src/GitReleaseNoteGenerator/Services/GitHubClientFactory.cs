// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net.Http.Headers;

using GitReleaseNoteGenerator.Infrastructure;

using Refit;

namespace GitReleaseNoteGenerator.Services;

/// <summary>Creates authenticated <see cref="IGitHubApi"/> clients backed by Refit and the source-generated JSON context.</summary>
public static class GitHubClientFactory
{
    /// <summary>The product name sent as the required GitHub API User-Agent.</summary>
    private const string ProductName = "GitReleaseNoteGenerator";

    /// <summary>The GitHub REST API base address.</summary>
    private static readonly Uri ApiBaseAddress = new("https://api.github.com/");

    /// <summary>Refit settings wired to the AOT-safe source-generated JSON context.</summary>
    private static readonly RefitSettings Settings = new(
        new SystemTextJsonContentSerializer(GitHubJsonContext.Default.Options));

    /// <summary>Creates a new <see cref="IGitHubApi"/> authenticated with the given token.</summary>
    /// <param name="token">The GitHub personal access token.</param>
    /// <returns>An authenticated GitHub API client.</returns>
    public static IGitHubApi Create(string token) =>
        Create(token, new HttpClientHandler());

    /// <summary>
    /// Creates an <see cref="IGitHubApi"/> whose transport is backed by the supplied
    /// <see cref="HttpMessageHandler"/>. This is the HTTP seam used to drive the API layer
    /// from tests with canned responses (and is equally usable for custom proxies/transports).
    /// </summary>
    /// <param name="token">The GitHub personal access token.</param>
    /// <param name="handler">The HTTP message handler that services API requests.</param>
    /// <returns>An authenticated GitHub API client using the supplied handler.</returns>
    public static IGitHubApi Create(string token, HttpMessageHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var httpClient = new HttpClient(handler, disposeHandler: false)
        {
            BaseAddress = ApiBaseAddress,
        };
        ConfigureDefaultHeaders(httpClient, token);

        return RestService.For<IGitHubApi>(httpClient, Settings);
    }

    /// <summary>Applies the GitHub-required User-Agent, media type, API version, and authorization headers.</summary>
    /// <param name="httpClient">The client to configure.</param>
    /// <param name="token">The GitHub personal access token.</param>
    private static void ConfigureDefaultHeaders(HttpClient httpClient, string token)
    {
        var headers = httpClient.DefaultRequestHeaders;
        headers.UserAgent.ParseAdd(ProductName);
        headers.Accept.ParseAdd("application/vnd.github+json");
        headers.Add("X-GitHub-Api-Version", "2022-11-28");

        if (string.IsNullOrEmpty(token))
        {
            return;
        }

        headers.Authorization = new("Bearer", token);
    }
}
