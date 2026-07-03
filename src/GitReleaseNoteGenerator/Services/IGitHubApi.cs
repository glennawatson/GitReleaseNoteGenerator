// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using GitReleaseNoteGenerator.Models;

using Refit;

namespace GitReleaseNoteGenerator.Services;

/// <summary>
/// The subset of the GitHub REST API used to generate release notes, described as a Refit
/// interface. Refit's source generator produces the trim- and AOT-friendly implementation.
/// </summary>
public interface IGitHubApi
{
    /// <summary>
    /// Gets a repository, used to resolve the default branch as the comparison head.
    /// </summary>
    /// <param name="owner">The repository owner.</param>
    /// <param name="repo">The repository name.</param>
    /// <returns>The repository payload.</returns>
    [Get("/repos/{owner}/{repo}")]
    Task<GitHubRepository> GetRepositoryAsync(string owner, string repo);

    /// <summary>
    /// Gets the latest published release, used to resolve the base ref when none is supplied.
    /// </summary>
    /// <param name="owner">The repository owner.</param>
    /// <param name="repo">The repository name.</param>
    /// <returns>The latest release payload.</returns>
    [Get("/repos/{owner}/{repo}/releases/latest")]
    Task<GitHubRelease> GetLatestReleaseAsync(string owner, string repo);

    /// <summary>
    /// Compares two refs and returns the commits between them.
    /// </summary>
    /// <param name="owner">The repository owner.</param>
    /// <param name="repo">The repository name.</param>
    /// <param name="basehead">The compare spec in the form "base...head".</param>
    /// <returns>The comparison payload.</returns>
    [Get("/repos/{owner}/{repo}/compare/{basehead}")]
    Task<GitHubComparison> CompareAsync(string owner, string repo, string basehead);

    /// <summary>
    /// Lists commits reachable from a ref, one page at a time.
    /// </summary>
    /// <param name="owner">The repository owner.</param>
    /// <param name="repo">The repository name.</param>
    /// <param name="sha">The SHA or ref to start listing from, or null for the default branch.</param>
    /// <param name="perPage">The page size (maximum 100).</param>
    /// <param name="page">The 1-based page number.</param>
    /// <returns>The commits on the requested page.</returns>
    [Get("/repos/{owner}/{repo}/commits")]
    Task<IReadOnlyList<GitHubCommit>> GetCommitsAsync(
        string owner,
        string repo,
        [AliasAs("sha")] string? sha,
        [AliasAs("per_page")] int perPage,
        [AliasAs("page")] int page);

    /// <summary>
    /// Searches for GitHub users, used to resolve a login from a real (non-noreply) email.
    /// </summary>
    /// <param name="query">The search query (for example, "user@example.com in:email").</param>
    /// <returns>The search result payload.</returns>
    [Get("/search/users")]
    Task<GitHubUserSearchResult> SearchUsersAsync([AliasAs("q")] string query);
}
