// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;

using GitReleaseNoteGenerator.Infrastructure;

using Microsoft.Extensions.Logging;

using Octokit;

using Polly;

namespace GitReleaseNoteGenerator.Services;

/// <summary>
/// Generates release notes by comparing a base ref (typically a tag from the latest release)
/// with a head ref (typically the default branch) using the GitHub Compare API.
/// </summary>
public sealed partial class ReleaseNoteGenerator
{
    /// <summary>
    /// The maximum number of pages to fetch when collecting historical authors.
    /// Prevents infinite pagination on very large repositories.
    /// </summary>
    private const int MaxPaginationPages = 500;

    /// <summary>
    /// The authenticated GitHub API client.
    /// </summary>
    private readonly GitHubClient _client;

    /// <summary>
    /// The logger for status and diagnostic messages.
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// The Polly resilience pipeline for retrying failed API calls.
    /// </summary>
    private readonly ResiliencePipeline _retry;

    /// <summary>
    /// Resolves commit contributors to canonical GitHub logins to avoid duplicate attribution.
    /// </summary>
    private readonly AuthorResolver _authorResolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReleaseNoteGenerator"/> class.
    /// </summary>
    /// <param name="client">An authenticated GitHub client.</param>
    /// <param name="logger">Logger for status messages.</param>
    public ReleaseNoteGenerator(GitHubClient client, ILogger logger)
    {
        _client = client;
        _logger = logger;
        _retry = RetryHandler.CreatePipeline(logger);
        _authorResolver = new(client, logger);
    }

    /// <summary>
    /// Generates release notes for the specified repository.
    /// </summary>
    /// <param name="owner">Repository owner.</param>
    /// <param name="repoName">Repository name.</param>
    /// <param name="version">The version string for the release.</param>
    /// <param name="baseRef">Optional base ref (tag) to compare from. If null, uses the latest release tag.</param>
    /// <param name="headRef">Optional head ref (branch) to compare to. If null, uses the default branch.</param>
    /// <returns>The generated release notes in Markdown format.</returns>
    public async Task<string> GenerateAsync(
        string owner,
        string repoName,
        string version,
        string? baseRef,
        string? headRef)
    {
        LogGenerating(owner, repoName, version);

        var repo = await _retry.ExecuteAsync(
            static async (state, _) => await state.Client.Repository.Get(state.Owner, state.RepoName).ConfigureAwait(false),
            (Client: _client, Owner: owner, RepoName: repoName),
            CancellationToken.None).ConfigureAwait(false);

        var resolvedBaseRef = await ResolveBaseRefAsync(owner, repoName, baseRef).ConfigureAwait(false);
        var resolvedHeadRef = headRef ?? repo.DefaultBranch;

        LogComparing(resolvedBaseRef ?? "(all history)", resolvedHeadRef);

        var commits = await GetCommitsAsync(owner, repoName, resolvedBaseRef, resolvedHeadRef).ConfigureAwait(false);

        LogCommitCount(commits.Count);

        var authorsAfterRelease = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var resolvedAuthorsByCommit = new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal);
        foreach (var commit in commits)
        {
            var resolved = await _authorResolver.GetResolvedAuthorsAsync(commit).ConfigureAwait(false);
            authorsAfterRelease.UnionWith(resolved);
            resolvedAuthorsByCommit[commit.Sha ?? string.Empty] = resolved;
        }

        var authorsBeforeRelease = await GetAllAuthorsReachableFromRefAsync(owner, repoName, resolvedBaseRef).ConfigureAwait(false);

        var newAuthors = new SortedSet<string>(
            authorsAfterRelease.Except(authorsBeforeRelease),
            StringComparer.OrdinalIgnoreCase);

        var groupedCommits = CommitCategorizer.GroupByCategory(commits);

        var fullChangelogUrl = !string.IsNullOrEmpty(resolvedBaseRef)
            ? string.Create(CultureInfo.InvariantCulture, $"https://github.com/{owner}/{repoName}/compare/{resolvedBaseRef}...{version}")
            : string.Create(CultureInfo.InvariantCulture, $"https://github.com/{owner}/{repoName}/commits/{version}");

        return FormatReleaseNotes(
            owner,
            repoName,
            fullChangelogUrl,
            authorsAfterRelease,
            newAuthors,
            groupedCommits,
            resolvedAuthorsByCommit);
    }

    /// <summary>
    /// Formats the release notes markdown from the provided inputs.
    /// </summary>
    /// <param name="ownerLogin">Repository owner login for commit links.</param>
    /// <param name="repoName">Repository name for commit links.</param>
    /// <param name="fullChangelogUrl">The URL to the GitHub compare view.</param>
    /// <param name="allAuthors">All contributors since the base ref.</param>
    /// <param name="newAuthors">Contributors since the base ref who did not appear earlier.</param>
    /// <param name="groupedCommits">Commits grouped by category.</param>
    /// <returns>Markdown-formatted release notes.</returns>
    internal static string FormatReleaseNotes(
        string ownerLogin,
        string repoName,
        string fullChangelogUrl,
        IEnumerable<string> allAuthors,
        IEnumerable<string> newAuthors,
        Dictionary<string, List<GitHubCommit>> groupedCommits) =>
        FormatReleaseNotes(
            ownerLogin,
            repoName,
            fullChangelogUrl,
            allAuthors,
            newAuthors,
            groupedCommits,
            null);

    /// <summary>
    /// Formats the release notes markdown from the provided inputs.
    /// </summary>
    /// <param name="ownerLogin">Repository owner login for commit links.</param>
    /// <param name="repoName">Repository name for commit links.</param>
    /// <param name="fullChangelogUrl">The URL to the GitHub compare view.</param>
    /// <param name="allAuthors">All contributors since the base ref.</param>
    /// <param name="newAuthors">Contributors since the base ref who did not appear earlier.</param>
    /// <param name="groupedCommits">Commits grouped by category.</param>
    /// <param name="resolvedAuthorsByCommit">
    /// Pre-resolved canonical authors keyed by commit SHA. When a commit is absent (or this map
    /// is null), the authors are resolved locally without an API call.
    /// </param>
    /// <returns>Markdown-formatted release notes.</returns>
    internal static string FormatReleaseNotes(
        string ownerLogin,
        string repoName,
        string fullChangelogUrl,
        IEnumerable<string> allAuthors,
        IEnumerable<string> newAuthors,
        Dictionary<string, List<GitHubCommit>> groupedCommits,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>>? resolvedAuthorsByCommit)
    {
        var sb = new StringBuilder()
            .AppendLine("## \U0001f5de\ufe0f What's Changed")
            .AppendLine();

        AppendChangedSections(sb, ownerLogin, repoName, groupedCommits, resolvedAuthorsByCommit);
        AppendFullChangelog(sb, fullChangelogUrl);
        AppendContributions(sb, allAuthors, newAuthors);

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Appends the grouped commit sections to the release notes.
    /// </summary>
    /// <param name="sb">The string builder to append to.</param>
    /// <param name="ownerLogin">Repository owner login for commit links.</param>
    /// <param name="repoName">Repository name for commit links.</param>
    /// <param name="groupedCommits">Commits grouped by category.</param>
    /// <param name="resolvedAuthorsByCommit">Pre-resolved canonical authors keyed by commit SHA.</param>
    private static void AppendChangedSections(
        StringBuilder sb,
        string ownerLogin,
        string repoName,
        Dictionary<string, List<GitHubCommit>> groupedCommits,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>>? resolvedAuthorsByCommit)
    {
        AppendKnownSections(sb, ownerLogin, repoName, groupedCommits, resolvedAuthorsByCommit);
        AppendOtherSection(sb, ownerLogin, repoName, groupedCommits, resolvedAuthorsByCommit);
        AppendCustomSections(sb, ownerLogin, repoName, groupedCommits, resolvedAuthorsByCommit);
    }

    /// <summary>
    /// Appends the full changelog URL to the release notes.
    /// </summary>
    /// <param name="sb">The string builder to append to.</param>
    /// <param name="fullChangelogUrl">The URL to the GitHub compare view.</param>
    private static void AppendFullChangelog(StringBuilder sb, string fullChangelogUrl) =>
        sb.Append("\U0001f517 **Full Changelog**: ")
            .AppendLine(fullChangelogUrl)
            .AppendLine();

    /// <summary>
    /// Appends contributor attribution to the release notes.
    /// </summary>
    /// <param name="sb">The string builder to append to.</param>
    /// <param name="allAuthors">All contributors since the base ref.</param>
    /// <param name="newAuthors">Contributors since the base ref who did not appear earlier.</param>
    private static void AppendContributions(StringBuilder sb, IEnumerable<string> allAuthors, IEnumerable<string> newAuthors)
    {
        var allAuthorsList = allAuthors.ToList();
        var newAuthorsList = newAuthors.ToList();
        var botContributors = allAuthorsList.Where(AuthorExtractor.IsBot).ToList();

        sb.AppendLine("### \U0001f64c Contributions");

        var nonBotNewAuthors = newAuthorsList.Except(botContributors, StringComparer.OrdinalIgnoreCase).ToList();
        if (nonBotNewAuthors.Count > 0)
        {
            sb.Append("\U0001f331 New contributors since the last release: ")
                .AppendJoin(", ", nonBotNewAuthors.Select(a => $"@{a}"))
                .AppendLine();
        }

        var nonBotContributors = allAuthorsList.Except(botContributors, StringComparer.OrdinalIgnoreCase).ToList();
        if (nonBotContributors.Count > 0)
        {
            sb.Append("\U0001f496 Thanks to all the contributors: ")
                .AppendJoin(", ", nonBotContributors.Select(a => $"@{a}"))
                .AppendLine();
        }

        if (botContributors.Count > 0)
        {
            sb.AppendLine()
                .Append("\U0001f916 Automated services that contributed: ")
                .AppendJoin(", ", botContributors.Select(a => $"@{a}"))
                .AppendLine();
        }
    }

    /// <summary>
    /// Appends release note sections for known categories in their configured order.
    /// </summary>
    /// <param name="sb">The string builder to append to.</param>
    /// <param name="ownerLogin">Repository owner login for commit links.</param>
    /// <param name="repoName">Repository name for commit links.</param>
    /// <param name="groupedCommits">Commits grouped by category.</param>
    /// <param name="resolvedAuthorsByCommit">Pre-resolved canonical authors keyed by commit SHA.</param>
    private static void AppendKnownSections(
        StringBuilder sb,
        string ownerLogin,
        string repoName,
        Dictionary<string, List<GitHubCommit>> groupedCommits,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>>? resolvedAuthorsByCommit)
    {
        foreach (var (_, categoryName, _) in CommitCategorizer.CategoryMap)
        {
            AppendSectionWhenPresent(sb, ownerLogin, repoName, groupedCommits, categoryName, resolvedAuthorsByCommit);
        }
    }

    /// <summary>
    /// Appends the fallback "Other Changes" release note section when present.
    /// </summary>
    /// <param name="sb">The string builder to append to.</param>
    /// <param name="ownerLogin">Repository owner login for commit links.</param>
    /// <param name="repoName">Repository name for commit links.</param>
    /// <param name="groupedCommits">Commits grouped by category.</param>
    /// <param name="resolvedAuthorsByCommit">Pre-resolved canonical authors keyed by commit SHA.</param>
    private static void AppendOtherSection(
        StringBuilder sb,
        string ownerLogin,
        string repoName,
        Dictionary<string, List<GitHubCommit>> groupedCommits,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>>? resolvedAuthorsByCommit) =>
        AppendSectionWhenPresent(
            sb,
            ownerLogin,
            repoName,
            groupedCommits,
            CommitCategorizer.CategoryMap.OtherCategory.Category,
            resolvedAuthorsByCommit);

    /// <summary>
    /// Appends release note sections that are not part of the configured category map.
    /// </summary>
    /// <param name="sb">The string builder to append to.</param>
    /// <param name="ownerLogin">Repository owner login for commit links.</param>
    /// <param name="repoName">Repository name for commit links.</param>
    /// <param name="groupedCommits">Commits grouped by category.</param>
    /// <param name="resolvedAuthorsByCommit">Pre-resolved canonical authors keyed by commit SHA.</param>
    private static void AppendCustomSections(
        StringBuilder sb,
        string ownerLogin,
        string repoName,
        Dictionary<string, List<GitHubCommit>> groupedCommits,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>>? resolvedAuthorsByCommit)
    {
        var customSections = groupedCommits.Where(kvp => IsCustomCategory(kvp.Key));

        foreach (var (category, commits) in customSections)
        {
            AppendSection(sb, category, commits, ownerLogin, repoName, resolvedAuthorsByCommit);
        }
    }

    /// <summary>
    /// Determines whether a category is outside the configured category map and fallback category.
    /// </summary>
    /// <param name="category">The category to inspect.</param>
    /// <returns>True when the category is custom; otherwise, false.</returns>
    private static bool IsCustomCategory(string category) =>
        !IsKnownCategory(category)
        && !string.Equals(
            category,
            CommitCategorizer.CategoryMap.OtherCategory.Category,
            StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Determines whether a category is part of the configured category map.
    /// </summary>
    /// <param name="category">The category to inspect.</param>
    /// <returns>True when the category is configured; otherwise, false.</returns>
    private static bool IsKnownCategory(string category) =>
        CommitCategorizer.CategoryMap
            .Any(t => string.Equals(t.Category, category, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Appends a release note section when it has commits.
    /// </summary>
    /// <param name="sb">The string builder to append to.</param>
    /// <param name="ownerLogin">Repository owner login for commit links.</param>
    /// <param name="repoName">Repository name for commit links.</param>
    /// <param name="groupedCommits">Commits grouped by category.</param>
    /// <param name="category">The category to append.</param>
    /// <param name="resolvedAuthorsByCommit">Pre-resolved canonical authors keyed by commit SHA.</param>
    private static void AppendSectionWhenPresent(
        StringBuilder sb,
        string ownerLogin,
        string repoName,
        Dictionary<string, List<GitHubCommit>> groupedCommits,
        string category,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>>? resolvedAuthorsByCommit)
    {
        if (!groupedCommits.TryGetValue(category, out var commits) || commits.Count == 0)
        {
            return;
        }

        AppendSection(sb, category, commits, ownerLogin, repoName, resolvedAuthorsByCommit);
    }

    /// <summary>
    /// Appends a release note section and trailing blank line.
    /// </summary>
    /// <param name="sb">The string builder to append to.</param>
    /// <param name="category">The category name for the section heading.</param>
    /// <param name="commits">The commits belonging to this category.</param>
    /// <param name="ownerLogin">Repository owner login for commit links.</param>
    /// <param name="repoName">Repository name for commit links.</param>
    /// <param name="resolvedAuthorsByCommit">Pre-resolved canonical authors keyed by commit SHA.</param>
    private static void AppendSection(
        StringBuilder sb,
        string category,
        IEnumerable<GitHubCommit> commits,
        string ownerLogin,
        string repoName,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>>? resolvedAuthorsByCommit)
    {
        FormatSection(sb, category, commits, ownerLogin, repoName, resolvedAuthorsByCommit);
        sb.AppendLine();
    }

    /// <summary>
    /// Appends a category section with its commits to the string builder.
    /// </summary>
    /// <param name="sb">The string builder to append to.</param>
    /// <param name="category">The category name for the section heading.</param>
    /// <param name="commits">The commits belonging to this category.</param>
    /// <param name="ownerLogin">The repository owner login for commit links.</param>
    /// <param name="repoName">The repository name for commit links.</param>
    /// <param name="resolvedAuthorsByCommit">Pre-resolved canonical authors keyed by commit SHA, or null.</param>
    private static void FormatSection(
        StringBuilder sb,
        string category,
        IEnumerable<GitHubCommit> commits,
        string ownerLogin,
        string repoName,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>>? resolvedAuthorsByCommit)
    {
        var emoji = CommitCategorizer.GetEmoji(category);
        sb.Append("### ")
            .Append(emoji)
            .Append(' ')
            .AppendLine(category);

        foreach (var commit in commits)
        {
            var message = commit.Commit.Message ?? string.Empty;
            var firstLine = message.Split(["\r\n", "\n"], StringSplitOptions.None)[0];

            var sha = commit.Sha ?? "unknown";
            var authors = resolvedAuthorsByCommit is not null
                && commit.Sha is not null
                && resolvedAuthorsByCommit.TryGetValue(commit.Sha, out var resolved)
                ? resolved
                : AuthorExtractor.GetCommitAuthors(commit);

            sb.Append(" * ")
                .Append(ownerLogin)
                .Append('/')
                .Append(repoName)
                .Append('@')
                .Append(sha)
                .Append(' ')
                .Append(firstLine)
                .Append(' ')
                .AppendJoin(' ', authors.Select(a => $"@{a}"))
                .AppendLine();
        }
    }

    /// <summary>
    /// Logs the start of release note generation.
    /// </summary>
    /// <param name="owner">The repository owner.</param>
    /// <param name="repo">The repository name.</param>
    /// <param name="version">The target version.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Generating release notes for {Owner}/{Repo} version {Version}")]
    private partial void LogGenerating(string owner, string repo, string version);

    /// <summary>
    /// Logs the base and head refs being compared.
    /// </summary>
    /// <param name="baseRef">The base ref (tag or branch).</param>
    /// <param name="headRef">The head ref (branch).</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Comparing {BaseRef} -> {HeadRef}")]
    private partial void LogComparing(string baseRef, string headRef);

    /// <summary>
    /// Logs the number of commits found since the last release.
    /// </summary>
    /// <param name="count">The number of commits.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Found {Count} commits since last release")]
    private partial void LogCommitCount(int count);

    /// <summary>
    /// Logs the tag name of the latest release.
    /// </summary>
    /// <param name="tagName">The release tag name.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Latest release: {TagName}")]
    private partial void LogLatestRelease(string? tagName);

    /// <summary>
    /// Logs that no existing releases were found for the repository.
    /// </summary>
    [LoggerMessage(Level = LogLevel.Information, Message = "No existing releases found - using entire commit history")]
    private partial void LogNoExistingReleases();

    /// <summary>
    /// Logs that the maximum pagination limit was reached while fetching historical authors.
    /// </summary>
    /// <param name="maxPages">The maximum page limit that was reached.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Reached max pagination limit ({MaxPages}) when fetching historical authors")]
    private partial void LogMaxPaginationReached(int maxPages);

    /// <summary>
    /// Resolves the base ref by using the provided value or falling back to the latest release tag.
    /// </summary>
    /// <param name="owner">The repository owner.</param>
    /// <param name="repoName">The repository name.</param>
    /// <param name="baseRef">The explicit base ref, or null to auto-detect.</param>
    /// <returns>The resolved base ref, or null if no releases exist.</returns>
    private async Task<string?> ResolveBaseRefAsync(string owner, string repoName, string? baseRef)
    {
        if (!string.IsNullOrWhiteSpace(baseRef))
        {
            return baseRef;
        }

        try
        {
            var latestRelease = await _retry.ExecuteAsync(
                static async (state, _) => await state.Client.Repository.Release.GetLatest(state.Owner, state.RepoName).ConfigureAwait(false),
                (Client: _client, Owner: owner, RepoName: repoName),
                CancellationToken.None).ConfigureAwait(false);

            LogLatestRelease(latestRelease?.TagName);
            return latestRelease?.TagName;
        }
        catch (NotFoundException)
        {
            LogNoExistingReleases();
            return null;
        }
    }

    /// <summary>
    /// Fetches commits between the base ref and head ref using the Compare or GetAll API.
    /// </summary>
    /// <param name="owner">The repository owner.</param>
    /// <param name="repoName">The repository name.</param>
    /// <param name="baseRef">The base ref to compare from, or null to get all commits.</param>
    /// <param name="headRef">The head ref to compare to.</param>
    /// <returns>The collection of commits between the two refs.</returns>
    private async Task<IReadOnlyCollection<GitHubCommit>> GetCommitsAsync(
        string owner,
        string repoName,
        string? baseRef,
        string headRef)
    {
        if (string.IsNullOrEmpty(baseRef))
        {
            return await _retry.ExecuteAsync(
                static async (state, _) => await state.Client.Repository.Commit
                    .GetAll(state.Owner, state.RepoName, new CommitRequest { Sha = state.HeadRef })
                    .ConfigureAwait(false),
                (Client: _client, Owner: owner, RepoName: repoName, HeadRef: headRef),
                CancellationToken.None).ConfigureAwait(false);
        }

        var comparison = await _retry.ExecuteAsync(
            static async (state, _) => await state.Client.Repository.Commit
                .Compare(state.Owner, state.RepoName, state.BaseRef, state.HeadRef)
                .ConfigureAwait(false),
            (Client: _client, Owner: owner, RepoName: repoName, BaseRef: baseRef, HeadRef: headRef),
            CancellationToken.None).ConfigureAwait(false);

        return comparison.Commits;
    }

    /// <summary>
    /// Fetches all authors reachable from a given ref by paginating through the commit history.
    /// </summary>
    /// <param name="owner">The repository owner.</param>
    /// <param name="repoName">The repository name.</param>
    /// <param name="refShaOrName">The ref SHA or name to start from, or null for the default branch.</param>
    /// <returns>A set of all normalized author identifiers found in the history.</returns>
    private async Task<HashSet<string>> GetAllAuthorsReachableFromRefAsync(
        string owner,
        string repoName,
        string? refShaOrName)
    {
        var authors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var page = 1;
        const int PageSize = 100;

        while (page <= MaxPaginationPages)
        {
            var localPage = page;
            var commits = await _retry.ExecuteAsync(
                static async (state, _) => await state.Client.Repository.Commit.GetAll(
                        state.Owner,
                        state.RepoName,
                        new() { Sha = state.RefShaOrName },
                        new()
                        {
                            PageCount = 1,
                            PageSize = PageSize,
                            StartPage = state.Page
                        })
                    .ConfigureAwait(false),
                (Client: _client, Owner: owner, RepoName: repoName, RefShaOrName: refShaOrName, Page: localPage),
                CancellationToken.None).ConfigureAwait(false);

            if (commits.Count == 0)
            {
                break;
            }

            foreach (var commit in commits)
            {
                authors.UnionWith(await _authorResolver.GetResolvedAuthorsAsync(commit).ConfigureAwait(false));
            }

            page++;
        }

        if (page > MaxPaginationPages)
        {
            LogMaxPaginationReached(MaxPaginationPages);
        }

        return authors;
    }
}
