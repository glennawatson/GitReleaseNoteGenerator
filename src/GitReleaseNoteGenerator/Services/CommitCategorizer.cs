// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Octokit;

namespace GitReleaseNoteGenerator.Services;

/// <summary>
/// Categorizes commits based on message prefixes and bot author overrides,
/// then groups them by category in priority order.
/// </summary>
public sealed class CommitCategorizer
{
    /// <summary>
    /// The default category name for commits that do not match any prefix.
    /// </summary>
    private const string OtherHeading = "Other";

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
        { OtherHeading, "\U0001f4cc" },
    };

    /// <summary>
    /// Maps known bot login names to the category prefix key used for trie lookup.
    /// </summary>
    private static readonly Dictionary<string, string> BotLoginToCategoryKey = new(StringComparer.OrdinalIgnoreCase)
    {
        { "renovate[bot]", "dep" },
        { "dependabot[bot]", "dep" },
        { "dependabot", "dep" },
    };

    /// <summary>
    /// Gets the category trie used for prefix-based categorization.
    /// </summary>
    public static CategoryTrie CategoryMap { get; } = new(
        OtherHeading,
        [
            (1, "Breaking Changes", new[] { "break" }),
            (2, "Features", new[] { "feat" }),
            (3, "Refactoring", new[] { "refactor" }),
            (4, "Fixes", new[] { "fix", "bug" }),
            (5, "Performance", new[] { "perf" }),
            (6, "General Changes", new[] { "housekeeping", "chore", "update" }),
            (7, "Tests", new[] { "test" }),
            (8, "Documentation", new[] { "doc" }),
            (9, "Style Changes", new[] { "style" }),
            (10, "Dependencies", new[] { "dep" }),
        ]);

    /// <summary>
    /// Gets the emoji for a given category name.
    /// </summary>
    /// <param name="category">The category name.</param>
    /// <returns>The emoji string, or a default pin emoji if unknown.</returns>
    public static string GetEmoji(string category) =>
        CategoryEmoji.TryGetValue(category, out var emoji) ? emoji : "\U0001f539";

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
        return CategoryMap[message];
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
}
