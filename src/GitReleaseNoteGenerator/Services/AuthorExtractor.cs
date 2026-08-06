// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

using GitReleaseNoteGenerator.Models;

namespace GitReleaseNoteGenerator.Services;

/// <summary>Extracts and normalizes author information from GitHub commits, including co-authors from commit message trailers.</summary>
public static class AuthorExtractor
{
    /// <summary>
    /// The suffix of a GitHub-provided "noreply" email address. The local part of such an
    /// address embeds the user's login (optionally prefixed by a numeric account ID and '+').
    /// </summary>
    private const string NoReplySuffix = "@users.noreply.github.com";

    /// <summary>The trailer key (case-insensitive) that identifies a co-author line in a commit message.</summary>
    private const string CoAuthorPrefix = "Co-authored-by:";

    /// <summary>The identifier used when a contributor carries no usable login, name, or email.</summary>
    private const string UnknownAuthor = "unknown";

    /// <summary>Line separator strings used to split commit messages into individual lines.</summary>
    private static readonly string[] LineSeparators = ["\r\n", "\n"];

    /// <summary>
    /// Extracts all author identifiers from a commit, including co-authors listed via
    /// "Co-authored-by:" trailer lines. Identities are resolved to GitHub logins where they
    /// can be determined locally (an API-resolved login or a GitHub noreply email); otherwise
    /// the normalized display name is used.
    /// </summary>
    /// <param name="commit">The GitHub commit to inspect.</param>
    /// <returns>A sorted set of normalized author identifiers.</returns>
    public static SortedSet<ContributorIdentity> GetCommitAuthors(GitHubCommit commit)
    {
        ArgumentNullException.ThrowIfNull(commit);

        var authors = new SortedSet<ContributorIdentity>(ContributorIdentity.ValueComparer);
        foreach (var contributor in GetContributors(commit))
        {
            _ = authors.Add(ResolveLocally(contributor));
        }

        return authors;
    }

    /// <summary>
    /// Extracts the structured contributor candidates from a commit: the primary author
    /// (from the GitHub-resolved login or git metadata) followed by any "Co-authored-by:"
    /// trailer entries. Identities are returned unresolved so callers may apply additional
    /// resolution (such as a GitHub API email lookup).
    /// </summary>
    /// <param name="commit">The GitHub commit to inspect.</param>
    /// <returns>The contributor candidates in the order they appear.</returns>
    public static IReadOnlyList<CommitContributor> GetContributors(GitHubCommit commit)
    {
        ArgumentNullException.ThrowIfNull(commit);

        var contributors = new List<CommitContributor> { GetPrimaryContributor(commit) };
        contributors.AddRange(GetCoAuthors(commit.Commit.Message));

        return contributors;
    }

    /// <summary>
    /// Resolves a contributor to an identifier using only locally-available information: an
    /// already-resolved login, a login embedded in a GitHub noreply email, or the normalized
    /// display name as a fallback.
    /// </summary>
    /// <param name="contributor">The contributor candidate to resolve.</param>
    /// <returns>The resolved identity, falling back to "unknown" if nothing usable is available.</returns>
    public static ContributorIdentity ResolveLocally(CommitContributor contributor)
    {
        ArgumentNullException.ThrowIfNull(contributor);

        if (!string.IsNullOrWhiteSpace(contributor.Login))
        {
            return ContributorIdentity.ForLogin(contributor.Login);
        }

        var embeddedLogin = TryGetLoginFromNoReplyEmail(contributor.Email);
        return embeddedLogin is not null
            ? ContributorIdentity.ForLogin(embeddedLogin)
            : ContributorIdentity.ForDisplayName(NormalizeAuthorName(contributor.Name ?? string.Empty));
    }

    /// <summary>
    /// Extracts the GitHub login embedded in a GitHub-provided "noreply" email address.
    /// Handles both the modern "ID+login@users.noreply.github.com" and the legacy
    /// "login@users.noreply.github.com" forms.
    /// </summary>
    /// <param name="email">The email address to inspect, or null.</param>
    /// <returns>The embedded login, or null if the email is not a GitHub noreply address.</returns>
    public static string? TryGetLoginFromNoReplyEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var trimmed = email.Trim();
        if (!trimmed.EndsWith(NoReplySuffix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var localPart = trimmed[..^NoReplySuffix.Length];
        var plusIndex = localPart.IndexOf('+', StringComparison.Ordinal);
        if (plusIndex >= 0)
        {
            localPart = localPart[(plusIndex + 1)..];
        }

        return string.IsNullOrWhiteSpace(localPart) ? null : localPart;
    }

    /// <summary>
    /// Normalizes an author string by removing the email portion and collapsing runs of whitespace.
    /// The display name is otherwise preserved: it could not be resolved to a GitHub login, so it is
    /// attributed as a person's name rather than squashed into a mention-shaped token.
    /// </summary>
    /// <param name="author">The raw author string (login, name, or "Name &lt;email&gt;").</param>
    /// <returns>A normalized identifier.</returns>
    public static string NormalizeAuthorName(string author)
    {
        if (string.IsNullOrWhiteSpace(author))
        {
            return UnknownAuthor;
        }

        var emailStart = author.IndexOf('<', StringComparison.Ordinal);
        if (emailStart >= 0)
        {
            author = author[..emailStart];
        }

        var normalized = CollapseWhitespace(author);
        return string.IsNullOrEmpty(normalized) ? UnknownAuthor : normalized;
    }

    /// <summary>Determines whether the given author identifier represents a bot account.</summary>
    /// <param name="author">The author identifier to check.</param>
    /// <returns>True if the author is a bot; otherwise, false.</returns>
    public static bool IsBot(string author)
    {
        ArgumentNullException.ThrowIfNull(author);

        return author.Contains("[bot]", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Trims the value and reduces every internal run of whitespace to a single space.</summary>
    /// <param name="value">The value to collapse.</param>
    /// <returns>The collapsed value.</returns>
    private static string CollapseWhitespace(string value)
    {
        var trimmed = value.AsSpan().Trim();
        var builder = new StringBuilder(trimmed.Length);
        var previousWasWhitespace = false;

        foreach (var character in trimmed)
        {
            if (char.IsWhiteSpace(character))
            {
                previousWasWhitespace = true;
                continue;
            }

            if (previousWasWhitespace && builder.Length > 0)
            {
                _ = builder.Append(' ');
            }

            previousWasWhitespace = false;
            _ = builder.Append(character);
        }

        return builder.ToString();
    }

    /// <summary>Gets the primary contributor from the GitHub-resolved author or git commit metadata.</summary>
    /// <param name="commit">The GitHub commit to inspect.</param>
    /// <returns>The primary contributor candidate.</returns>
    private static CommitContributor GetPrimaryContributor(GitHubCommit commit) =>
        new(
            commit.Author?.Login ?? commit.Committer?.Login,
            commit.Commit.Author?.Name ?? commit.Commit.Committer?.Name,
            commit.Commit.Author?.Email ?? commit.Commit.Committer?.Email);

    /// <summary>Gets co-author contributors from commit message trailer lines.</summary>
    /// <param name="commitMessage">The commit message to inspect.</param>
    /// <returns>The co-author contributor candidates in message order.</returns>
    private static IEnumerable<CommitContributor> GetCoAuthors(string? commitMessage)
    {
        foreach (var line in GetCoAuthorLines(commitMessage))
        {
            yield return ParseCoAuthor(line[CoAuthorPrefix.Length..].Trim());
        }
    }

    /// <summary>Gets co-author trailer lines from a commit message.</summary>
    /// <param name="commitMessage">The commit message to inspect.</param>
    /// <returns>The trimmed co-author trailer lines.</returns>
    private static IEnumerable<string> GetCoAuthorLines(string? commitMessage)
    {
        foreach (var line in (commitMessage ?? string.Empty).Split(LineSeparators, StringSplitOptions.None))
        {
            var trimmedLine = line.Trim();
            if (trimmedLine.StartsWith(CoAuthorPrefix, StringComparison.OrdinalIgnoreCase))
            {
                yield return trimmedLine;
            }
        }
    }

    /// <summary>Parses a raw "Co-authored-by:" value of the form "Name &lt;email&gt;" into a contributor.</summary>
    /// <param name="value">The trailer value with the key already removed.</param>
    /// <returns>A contributor candidate carrying the parsed name and email.</returns>
    private static CommitContributor ParseCoAuthor(string value)
    {
        var emailStart = value.IndexOf('<', StringComparison.Ordinal);
        if (emailStart < 0)
        {
            return new(null, value.Trim(), null);
        }

        var name = value[..emailStart].Trim();
        var emailEnd = value.IndexOf('>', emailStart);
        var email = emailEnd > emailStart
            ? value[(emailStart + 1)..emailEnd].Trim()
            : value[(emailStart + 1)..].Trim();

        return new(null, name, email);
    }
}
