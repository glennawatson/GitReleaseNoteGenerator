// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;

using GitReleaseNoteGenerator.Infrastructure;
using GitReleaseNoteGenerator.Models;

using Microsoft.Extensions.Logging;

namespace GitReleaseNoteGenerator.Services;

/// <summary>The production <see cref="IUserLoginSearch"/> implementation backed by the GitHub "search users by email" API, wrapped in the shared retry pipeline.</summary>
[System.Diagnostics.DebuggerDisplay("GitHubUserLoginSearch: {_api}")]
public sealed class GitHubUserLoginSearch : IUserLoginSearch
{
    /// <summary>The authenticated GitHub API client.</summary>
    private readonly IGitHubApi _api;

    /// <summary>Retries failed API calls.</summary>
    private readonly RetryHandler _retry;

    /// <summary>Initializes a new instance of the <see cref="GitHubUserLoginSearch"/> class.</summary>
    /// <param name="api">An authenticated GitHub API client.</param>
    /// <param name="logger">Logger for retry diagnostics.</param>
    public GitHubUserLoginSearch(IGitHubApi api, ILogger logger)
    {
        _api = api;
        _retry = new(logger);
    }

    /// <inheritdoc/>
    public async Task<string?> FindLoginByEmailAsync(string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        var query = string.Create(CultureInfo.InvariantCulture, $"{email} in:email");

        var result = await _retry.ExecuteAsync(
            static async (state, _) => await state.Api.SearchUsersAsync(state.Query).ConfigureAwait(false),
            (Api: _api, Query: query),
            CancellationToken.None).ConfigureAwait(false);

        return GetFirstLogin(result);
    }

    /// <summary>Gets the first login from a user search result.</summary>
    /// <param name="result">The search result to inspect.</param>
    /// <returns>The first user login, or null when no users were found.</returns>
    private static string? GetFirstLogin(GitHubUserSearchResult result) =>
        result.Items is { Count: > 0 } items ? items[0].Login : null;
}
