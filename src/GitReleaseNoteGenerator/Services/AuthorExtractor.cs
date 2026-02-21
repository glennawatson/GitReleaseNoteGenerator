// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Octokit;

namespace GitReleaseNoteGenerator.Services;

/// <summary>
/// Extracts and normalizes author information from GitHub commits,
/// including co-authors from commit message trailers.
/// </summary>
public static class AuthorExtractor
{
    /// <summary>
    /// Line separator strings used to split commit messages into individual lines.
    /// </summary>
    private static readonly string[] LineSeparators = ["\r\n", "\n"];

    /// <summary>
    /// Extracts all author identifiers from a commit, including co-authors listed via
    /// "Co-authored-by:" trailer lines.
    /// </summary>
    /// <param name="commit">The GitHub commit to inspect.</param>
    /// <returns>A sorted set of normalized author identifiers.</returns>
    public static SortedSet<string> GetCommitAuthors(GitHubCommit commit)
    {
        ArgumentNullException.ThrowIfNull(commit);

        var authors = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        var authorLogin = commit.Author?.Login ?? commit.Committer?.Login;
        var primary = authorLogin ?? commit.Commit.Author?.Name ?? commit.Commit.Committer?.Name ?? "unknown";
        authors.Add(NormalizeAuthorName(primary));

        var commitMessage = commit.Commit.Message ?? string.Empty;
        var lines = commitMessage.Split(LineSeparators, StringSplitOptions.None);
        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            if (trimmedLine.StartsWith("Co-authored-by:", StringComparison.OrdinalIgnoreCase))
            {
                var coAuthor = trimmedLine["Co-authored-by:".Length..].Trim();
                authors.Add(NormalizeAuthorName(coAuthor));
            }
        }

        return authors;
    }

    /// <summary>
    /// Normalizes an author string by removing the email portion and whitespace.
    /// </summary>
    /// <param name="author">The raw author string (login, name, or "Name &lt;email&gt;").</param>
    /// <returns>A normalized identifier.</returns>
    public static string NormalizeAuthorName(string author)
    {
        if (string.IsNullOrWhiteSpace(author))
        {
            return "unknown";
        }

        var emailStart = author.IndexOf('<', StringComparison.Ordinal);
        if (emailStart >= 0)
        {
            author = author[..emailStart];
        }

        var normalized = author.Replace(" ", string.Empty, StringComparison.Ordinal).Trim();
        return string.IsNullOrEmpty(normalized) ? "unknown" : normalized;
    }

    /// <summary>
    /// Determines whether the given author identifier represents a bot account.
    /// </summary>
    /// <param name="author">The author identifier to check.</param>
    /// <returns>True if the author is a bot; otherwise, false.</returns>
    public static bool IsBot(string author)
    {
        ArgumentNullException.ThrowIfNull(author);

        return author.Contains("[bot]", StringComparison.OrdinalIgnoreCase);
    }
}
