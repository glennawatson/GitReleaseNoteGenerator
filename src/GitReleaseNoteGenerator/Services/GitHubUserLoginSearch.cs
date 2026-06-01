// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using GitReleaseNoteGenerator.Infrastructure;

using Microsoft.Extensions.Logging;

using Octokit;

using Polly;

namespace GitReleaseNoteGenerator.Services;

/// <summary>
/// The production <see cref="IUserLoginSearch"/> implementation backed by the GitHub
/// "search users by email" API, wrapped in the shared retry pipeline.
/// </summary>
public sealed class GitHubUserLoginSearch : IUserLoginSearch
{
    /// <summary>
    /// The authenticated GitHub API client.
    /// </summary>
    private readonly GitHubClient _client;

    /// <summary>
    /// The Polly resilience pipeline for retrying failed API calls.
    /// </summary>
    private readonly ResiliencePipeline _retry;

    /// <summary>
    /// Initializes a new instance of the <see cref="GitHubUserLoginSearch"/> class.
    /// </summary>
    /// <param name="client">An authenticated GitHub client.</param>
    /// <param name="logger">Logger for retry diagnostics.</param>
    public GitHubUserLoginSearch(GitHubClient client, ILogger logger)
    {
        _client = client;
        _retry = RetryHandler.CreatePipeline(logger);
    }

    /// <inheritdoc/>
    public async Task<string?> FindLoginByEmailAsync(string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        var request = new SearchUsersRequest(email)
        {
            In = [UserInQualifier.Email]
        };

        var result = await _retry.ExecuteAsync(
            static async (state, _) => await state.Client.Search.SearchUsers(state.Request).ConfigureAwait(false),
            (Client: _client, Request: request),
            CancellationToken.None).ConfigureAwait(false);

        return GetFirstLogin(result);
    }

    /// <summary>
    /// Gets the first login from a user search result.
    /// </summary>
    /// <param name="result">The search result to inspect.</param>
    /// <returns>The first user login, or null when no users were found.</returns>
    private static string? GetFirstLogin(SearchUsersResult result) =>
        result.Items.Count == 0 ? null : result.Items[0].Login;
}
