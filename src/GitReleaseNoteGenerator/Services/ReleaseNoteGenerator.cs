// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;

using GitReleaseNoteGenerator.Infrastructure;
using GitReleaseNoteGenerator.Models;

using Microsoft.Extensions.Logging;

using Polly;

using Refit;

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

    /// <summary>The number of commits requested per page (the GitHub maximum).</summary>
    private const int CommitsPageSize = 100;

    /// <summary>The fallback head ref used when neither an explicit head ref nor a repository default branch is available.</summary>
    private const string DefaultHeadRef = "HEAD";

    /// <summary>The authenticated GitHub API client.</summary>
    private readonly IGitHubApi _api;

    /// <summary>The logger for status and diagnostic messages.</summary>
    private readonly ILogger _logger;

    /// <summary>The Polly resilience pipeline for retrying failed API calls.</summary>
    private readonly ResiliencePipeline _retry;

    /// <summary>Resolves commit contributors to canonical GitHub logins to avoid duplicate attribution.</summary>
    private readonly AuthorResolver _authorResolver;

    /// <summary>Initializes a new instance of the <see cref="ReleaseNoteGenerator"/> class.</summary>
    /// <param name="api">An authenticated GitHub API client.</param>
    /// <param name="logger">Logger for status messages.</param>
    public ReleaseNoteGenerator(IGitHubApi api, ILogger logger)
    {
        _api = api;
        _logger = logger;
        _retry = RetryHandler.CreatePipeline(logger);
        _authorResolver = new(api, logger);
    }

    /// <summary>Generates release notes for the specified repository.</summary>
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
            static async (state, _) => await state.Api.GetRepositoryAsync(state.Owner, state.RepoName).ConfigureAwait(false),
            (Api: _api, Owner: owner, RepoName: repoName),
            CancellationToken.None).ConfigureAwait(false);

        var resolvedBaseRef = await ResolveBaseRefAsync(owner, repoName, baseRef).ConfigureAwait(false);
        var resolvedHeadRef = headRef ?? repo.DefaultBranch ?? DefaultHeadRef;

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

        var newAuthors = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var author in authorsAfterRelease)
        {
            if (!authorsBeforeRelease.Contains(author))
            {
                _ = newAuthors.Add(author);
            }
        }

        var groupedCommits = CommitCategorizer.GroupByCategory(commits);

        var headTag = AlignVersionWithBaseRefPrefix(version, resolvedBaseRef);
        var fullChangelogUrl = !string.IsNullOrEmpty(resolvedBaseRef)
            ? string.Create(CultureInfo.InvariantCulture, $"https://github.com/{owner}/{repoName}/compare/{resolvedBaseRef}...{headTag}")
            : string.Create(CultureInfo.InvariantCulture, $"https://github.com/{owner}/{repoName}/commits/{headTag}");

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
    /// Aligns the release version with the base ref's tag-prefix convention so the generated
    /// changelog link points at a tag that actually exists. Version tools (nbgv, MinVer, and the
    /// like) typically emit a bare semantic version such as "10.0.0", while a repository's tags —
    /// including the previous release used as the base ref — often carry a "v" prefix ("v10.0.0").
    /// When the base ref is "v"-prefixed and the version is not, the same prefix is applied so the
    /// compare view resolves (for example, ".../compare/v9.0.0...v10.0.0" rather than
    /// ".../compare/v9.0.0...10.0.0"). When no base ref is available the version is returned as-is,
    /// since there is no existing tag from which to infer the convention.
    /// </summary>
    /// <param name="version">The release version supplied on the command line.</param>
    /// <param name="baseRef">The resolved base ref (previous release tag), or null.</param>
    /// <returns>The version aligned to the base ref's tag prefix.</returns>
    internal static string AlignVersionWithBaseRefPrefix(string version, string? baseRef)
    {
        return string.IsNullOrEmpty(version)
            || string.IsNullOrEmpty(baseRef)
            || !HasVersionPrefix(baseRef)
            || HasVersionPrefix(version) ? version : string.Concat(baseRef.AsSpan(0, 1), version);
    }

    /// <summary>Formats the release notes markdown from the provided inputs.</summary>
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

    /// <summary>Formats the release notes markdown from the provided inputs.</summary>
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
    /// Determines whether a tag or version begins with a "v"/"V" prefix immediately followed by a
    /// digit (for example, "v10.0.0"), distinguishing a version prefix from a branch or ref that
    /// merely happens to start with the letter "v".
    /// </summary>
    /// <param name="value">The tag or version to inspect.</param>
    /// <returns>True when the value carries a version prefix; otherwise, false.</returns>
    private static bool HasVersionPrefix(string value) =>
        value.Length >= 2 && value[0] is 'v' or 'V' && char.IsAsciiDigit(value[1]);

    /// <summary>Appends the grouped commit sections to the release notes.</summary>
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

    /// <summary>Appends the full changelog URL to the release notes.</summary>
    /// <param name="sb">The string builder to append to.</param>
    /// <param name="fullChangelogUrl">The URL to the GitHub compare view.</param>
    private static void AppendFullChangelog(StringBuilder sb, string fullChangelogUrl) =>
        sb.Append("\U0001f517 **Full Changelog**: ")
            .AppendLine(fullChangelogUrl)
            .AppendLine();

    /// <summary>Appends contributor attribution to the release notes.</summary>
    /// <param name="sb">The string builder to append to.</param>
    /// <param name="allAuthors">All contributors since the base ref.</param>
    /// <param name="newAuthors">Contributors since the base ref who did not appear earlier.</param>
    private static void AppendContributions(StringBuilder sb, IEnumerable<string> allAuthors, IEnumerable<string> newAuthors)
    {
        var allAuthorsList = new List<string>(allAuthors);
        var newAuthorsList = new List<string>(newAuthors);

        var botContributors = new List<string>();
        foreach (var author in allAuthorsList)
        {
            if (AuthorExtractor.IsBot(author))
            {
                botContributors.Add(author);
            }
        }

        _ = sb.AppendLine("### \U0001f64c Contributions");

        var botSet = new HashSet<string>(botContributors, StringComparer.OrdinalIgnoreCase);

        var nonBotNewAuthors = FilterOut(newAuthorsList, botSet);
        if (nonBotNewAuthors.Count > 0)
        {
            _ = sb.Append("\U0001f331 New contributors since the last release: ");
            AppendMentions(sb, ", ", nonBotNewAuthors);
            _ = sb.AppendLine();
        }

        var nonBotContributors = FilterOut(allAuthorsList, botSet);
        if (nonBotContributors.Count > 0)
        {
            _ = sb.Append("\U0001f496 Thanks to all the contributors: ");
            AppendMentions(sb, ", ", nonBotContributors);
            _ = sb.AppendLine();
        }

        if (botContributors.Count == 0)
        {
            return;
        }

        _ = sb.AppendLine()
            .Append("\U0001f916 Automated services that contributed: ");
        AppendMentions(sb, ", ", botContributors);
        _ = sb.AppendLine();
    }

    /// <summary>Returns the authors from <paramref name="authors"/> that are not present in <paramref name="excluded"/>.</summary>
    /// <param name="authors">The candidate authors, already de-duplicated.</param>
    /// <param name="excluded">The set of authors to omit (case-insensitive).</param>
    /// <returns>The retained authors, in their original order.</returns>
    private static List<string> FilterOut(List<string> authors, HashSet<string> excluded)
    {
        var retained = new List<string>();
        foreach (var author in authors)
        {
            if (!excluded.Contains(author))
            {
                retained.Add(author);
            }
        }

        return retained;
    }

    /// <summary>Appends each author as an <c>@mention</c>, separated by <paramref name="separator"/>.</summary>
    /// <param name="sb">The string builder to append to.</param>
    /// <param name="separator">The separator placed between mentions.</param>
    /// <param name="names">The author names to mention.</param>
    private static void AppendMentions(StringBuilder sb, string separator, IEnumerable<string> names)
    {
        var first = true;
        foreach (var name in names)
        {
            if (!first)
            {
                _ = sb.Append(separator);
            }

            _ = sb.Append('@').Append(name);
            first = false;
        }
    }

    /// <summary>Appends release note sections for known categories in their configured order.</summary>
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

    /// <summary>Appends the fallback "Other Changes" release note section when present.</summary>
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

    /// <summary>Appends release note sections that are not part of the configured category map.</summary>
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
        foreach (var (category, commits) in groupedCommits)
        {
            if (!IsCustomCategory(category))
            {
                continue;
            }

            AppendSection(sb, category, commits, ownerLogin, repoName, resolvedAuthorsByCommit);
        }
    }

    /// <summary>Determines whether a category is outside the configured category map and fallback category.</summary>
    /// <param name="category">The category to inspect.</param>
    /// <returns>True when the category is custom; otherwise, false.</returns>
    private static bool IsCustomCategory(string category) =>
        !IsKnownCategory(category)
        && !string.Equals(
            category,
            CommitCategorizer.CategoryMap.OtherCategory.Category,
            StringComparison.OrdinalIgnoreCase);

    /// <summary>Determines whether a category is part of the configured category map.</summary>
    /// <param name="category">The category to inspect.</param>
    /// <returns>True when the category is configured; otherwise, false.</returns>
    private static bool IsKnownCategory(string category)
    {
        foreach (var entry in CommitCategorizer.CategoryMap)
        {
            if (string.Equals(entry.Category, category, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Appends a release note section when it has commits.</summary>
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

    /// <summary>Appends a release note section and trailing blank line.</summary>
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
        _ = sb.AppendLine();
    }

    /// <summary>Appends a category section with its commits to the string builder.</summary>
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
        _ = sb.Append("### ")
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

            _ = sb.Append(" * ")
                .Append(ownerLogin)
                .Append('/')
                .Append(repoName)
                .Append('@')
                .Append(sha)
                .Append(' ')
                .Append(firstLine)
                .Append(' ');
            AppendMentions(sb, " ", authors);
            _ = sb.AppendLine();
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

    /// <summary>Resolves the base ref by using the provided value or falling back to the latest release tag.</summary>
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
                static async (state, _) => await state.Api.GetLatestReleaseAsync(state.Owner, state.RepoName).ConfigureAwait(false),
                (Api: _api, Owner: owner, RepoName: repoName),
                CancellationToken.None).ConfigureAwait(false);

            LogLatestRelease(latestRelease?.TagName);
            return latestRelease?.TagName;
        }
        catch (ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            LogNoExistingReleases();
            return null;
        }
    }

    /// <summary>Fetches commits between the base ref and head ref using the Compare or GetAll API.</summary>
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
            return await GetAllCommitsAsync(owner, repoName, headRef).ConfigureAwait(false);
        }

        var basehead = $"{baseRef}...{headRef}";
        var comparison = await _retry.ExecuteAsync(
            static async (state, _) => await state.Api
                .CompareAsync(state.Owner, state.RepoName, state.BaseHead)
                .ConfigureAwait(false),
            (Api: _api, Owner: owner, RepoName: repoName, BaseHead: basehead),
            CancellationToken.None).ConfigureAwait(false);

        return comparison.Commits ?? [];
    }

    /// <summary>
    /// Fetches every commit reachable from a head ref by paginating the commits API. Used when no
    /// base ref exists (the repository has no releases yet) so the entire history forms the notes.
    /// </summary>
    /// <param name="owner">The repository owner.</param>
    /// <param name="repoName">The repository name.</param>
    /// <param name="headRef">The head ref to list commits from.</param>
    /// <returns>All commits reachable from the head ref, up to the pagination cap.</returns>
    private async Task<IReadOnlyCollection<GitHubCommit>> GetAllCommitsAsync(
        string owner,
        string repoName,
        string headRef)
    {
        var all = new List<GitHubCommit>();
        var page = 1;

        while (page <= MaxPaginationPages)
        {
            var commits = await FetchCommitPageAsync(owner, repoName, headRef, page).ConfigureAwait(false);
            if (commits.Count == 0)
            {
                break;
            }

            all.AddRange(commits);
            page++;
        }

        if (page > MaxPaginationPages)
        {
            LogMaxPaginationReached(MaxPaginationPages);
        }

        return all;
    }

    /// <summary>Fetches a single page of commits reachable from a ref, wrapped in the shared retry pipeline.</summary>
    /// <param name="owner">The repository owner.</param>
    /// <param name="repoName">The repository name.</param>
    /// <param name="sha">The ref SHA or name to list from, or null for the default branch.</param>
    /// <param name="page">The 1-based page number.</param>
    /// <returns>The commits on the requested page.</returns>
    private async Task<IReadOnlyList<GitHubCommit>> FetchCommitPageAsync(
        string owner,
        string repoName,
        string? sha,
        int page) =>
        await _retry.ExecuteAsync(
            static async (state, _) => await state.Api
                .GetCommitsAsync(state.Owner, state.RepoName, state.Sha, CommitsPageSize, state.Page)
                .ConfigureAwait(false),
            (Api: _api, Owner: owner, RepoName: repoName, Sha: sha, Page: page),
            CancellationToken.None).ConfigureAwait(false);

    /// <summary>Fetches all authors reachable from a given ref by paginating through the commit history.</summary>
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

        while (page <= MaxPaginationPages)
        {
            var commits = await FetchCommitPageAsync(owner, repoName, refShaOrName, page).ConfigureAwait(false);

            if (commits.Count == 0)
            {
                break;
            }

            // Resolve cache-only: the (small) set of commits since the last release has already
            // been resolved with the search API, priming the email->login cache. Walking the full
            // history — potentially tens of thousands of commits — must not issue a search request
            // per historical contributor, or the strict search rate limit is quickly exhausted.
            foreach (var commit in commits)
            {
                authors.UnionWith(await _authorResolver.GetResolvedAuthorsAsync(commit, allowSearch: false).ConfigureAwait(false));
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
