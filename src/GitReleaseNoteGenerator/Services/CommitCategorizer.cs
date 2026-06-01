// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

using Octokit;

namespace GitReleaseNoteGenerator.Services;

/// <summary>
/// Categorizes commits according to the Conventional Commits specification
/// (https://www.conventionalcommits.org), with bot author overrides, then groups them by
/// category in priority order.
/// </summary>
internal static partial class CommitCategorizer
{
    /// <summary>
    /// The default category name for commits that do not match any prefix.
    /// </summary>
    private const string OtherHeading = "Other";

    /// <summary>
    /// The trie key whose lookup yields the canonical "Breaking Changes" category tuple.
    /// </summary>
    private const string BreakingChangeKey = "break";

    /// <summary>
    /// Line separator strings used to split commit messages into individual lines.
    /// </summary>
    private static readonly string[] LineSeparators = ["\r\n", "\n"];

    /// <summary>
    /// Maps category names to their emoji characters used in release note headings.
    /// </summary>
    private static readonly Dictionary<string, string> CategoryEmoji = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Breaking Changes", "\U0001f4a5" },
        { "Features", "\u2728" },
        { "Refactoring", "\u267b\ufe0f" },
        { "Fixes", "\U0001f41b" },
        { "Performance", "\u26a1" },
        { "General Changes", "\U0001f9f9" },
        { "Tests", "\u2705" },
        { "Documentation", "\U0001f4dd" },
        { "Style Changes", "\U0001f485" },
        { "Dependencies", "\U0001f4e6" },
        { OtherHeading, "\U0001f4cc" }
    };

    /// <summary>
    /// Maps known bot login names to the category prefix key used for trie lookup.
    /// </summary>
    private static readonly Dictionary<string, string> BotLoginToCategoryKey = new(StringComparer.OrdinalIgnoreCase)
    {
        { "renovate[bot]", "dep" },
        { "dependabot[bot]", "dep" },
        { "dependabot", "dep" }
    };

    /// <summary>
    /// Gets the category trie used for prefix-based categorization.
    /// </summary>
    [SuppressMessage(
        "Major Code Smell",
        "S109:Magic numbers should not be used",
        Justification = "Trie structure is defined by the Conventional Commits spec.")]
    internal static CategoryTrie CategoryMap { get; } = new(
        OtherHeading,
        [
            new(1, "Breaking Changes", ["break"]),
            new(2, "Features", ["feat"]),
            new(3, "Refactoring", ["refactor"]),
            new(4, "Fixes", ["fix", "bug"]),
            new(5, "Performance", ["perf"]),
            new(6, "General Changes", ["housekeeping", "chore", "update", "build", "ci", "revert"]),
            new(7, "Tests", ["test"]),
            new(8, "Documentation", ["doc"]),
            new(9, "Style Changes", ["style"]),
            new(10, "Dependencies", ["dep"])
        ]);

    /// <summary>
    /// Gets the emoji for a given category name.
    /// </summary>
    /// <param name="category">The category name.</param>
    /// <returns>The emoji string, or a default pin emoji if unknown.</returns>
    public static string GetEmoji(string category) =>
        CategoryEmoji.GetValueOrDefault(category, "\U0001f539");

    /// <summary>
    /// Categorizes a single commit using bot overrides first, then message-based prefix matching.
    /// </summary>
    /// <param name="commit">The commit to categorize.</param>
    /// <returns>The priority and category name.</returns>
    public static (int Priority, string Category) CategorizeCommit(GitHubCommit commit)
    {
        ArgumentNullException.ThrowIfNull(commit);

        var login = commit.Author?.Login ?? commit.Committer?.Login;

        if (!string.IsNullOrEmpty(login) && BotLoginToCategoryKey.TryGetValue(login, out var categoryKey))
        {
            return CategoryMap[categoryKey];
        }

        var message = commit.Commit.Message ?? string.Empty;
        return CategorizeMessage(message);
    }

    /// <summary>
    /// Groups commits by category, ordered by category priority.
    /// </summary>
    /// <param name="commits">The commits to group.</param>
    /// <returns>A dictionary mapping category names to their commits, in priority order.</returns>
    public static Dictionary<string, List<GitHubCommit>> GroupByCategory(IEnumerable<GitHubCommit> commits)
    {
        ArgumentNullException.ThrowIfNull(commits);

        var groupedCommits = new Dictionary<string, List<GitHubCommit>>(StringComparer.OrdinalIgnoreCase);
        var priorityQueue = new PriorityQueue<(int Priority, string Category, GitHubCommit Commit), int>();

        foreach (var commit in commits)
        {
            var (priority, category) = CategorizeCommit(commit);
            priorityQueue.Enqueue((priority, category, commit), priority);
        }

        while (priorityQueue.Count > 0)
        {
            var (_, category, commit) = priorityQueue.Dequeue();

            if (!groupedCommits.TryGetValue(category, out var list))
            {
                list = [];
                groupedCommits[category] = list;
            }

            list.Add(commit);
        }

        return groupedCommits;
    }

    /// <summary>
    /// Categorizes a commit message according to the Conventional Commits specification.
    /// The first line is parsed as "type(scope)!: description"; a "!" marker or a
    /// "BREAKING CHANGE:" footer promotes the commit to Breaking Changes. Messages that are
    /// not valid conventional commits, or whose type is unrecognized, fall back to Other.
    /// </summary>
    /// <param name="message">The full commit message.</param>
    /// <returns>The priority and category name.</returns>
    internal static (int Priority, string Category) CategorizeMessage(string message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var lines = message.Split(LineSeparators, StringSplitOptions.None);
        var firstLine = lines.Length > 0 ? lines[0] : string.Empty;

        var match = ConventionalHeaderRegex().Match(firstLine);
        if (!match.Success)
        {
            return CategoryMap.OtherCategory;
        }

        if (match.Groups["breaking"].Success || HasBreakingChangeFooter(lines))
        {
            return CategoryMap[BreakingChangeKey];
        }

        return CategoryMap[match.Groups["type"].Value];
    }

    /// <summary>
    /// Determines whether any line of the commit message is a "BREAKING CHANGE:" (or
    /// "BREAKING-CHANGE:") footer, as defined by the Conventional Commits specification.
    /// </summary>
    /// <param name="lines">The individual lines of the commit message.</param>
    /// <returns>True if a breaking-change footer is present; otherwise, false.</returns>
    private static bool HasBreakingChangeFooter(string[] lines)
    {
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("BREAKING CHANGE:", StringComparison.Ordinal)
                || trimmed.StartsWith("BREAKING-CHANGE:", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets the compiled regular expression that matches a Conventional Commits header
    /// of the form "type(scope)!:", capturing the type and an optional breaking-change marker.
    /// </summary>
    /// <returns>The compiled header-matching regular expression.</returns>
    [GeneratedRegex(@"^(?<type>[a-zA-Z]+)(?:\((?<scope>[^)]*)\))?(?<breaking>!)?:", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ConventionalHeaderRegex();
}
