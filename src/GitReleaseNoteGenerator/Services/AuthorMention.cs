// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

using GitReleaseNoteGenerator.Models;

namespace GitReleaseNoteGenerator.Services;

/// <summary>
/// Renders a resolved contributor into release note Markdown. A contributor becomes an
/// <c>@mention</c> only when resolution confirmed a GitHub login and that login is syntactically
/// mentionable; anything else — an unresolved display name, or a bot login such as
/// <c>renovate[bot]</c> — is written as literal text with its Markdown metacharacters escaped.
/// </summary>
/// <remarks>
/// Mentioning indiscriminately produces three defects, and shape alone catches only two of them.
/// A display name carrying characters a login cannot hold renders as a mention resolving to nobody.
/// A bot login fares worse: Markdown consumes the "[bot]" as a link, leaving the leading
/// "@renovate" to autolink to an unrelated human account. The third is invisible in the output and
/// so the most damaging — a display name that merely looks like a login ("Lukas") credits whichever
/// stranger holds that account. Only <see cref="ContributorIdentity.IsLogin"/> distinguishes it.
/// </remarks>
internal static class AuthorMention
{
    /// <summary>The maximum length of a GitHub login.</summary>
    private const int MaxLoginLength = 39;

    /// <summary>
    /// The Markdown metacharacters escaped when an identifier is written as literal text. Square
    /// brackets matter most here — they are what breaks bot attribution — but emphasis, code, and
    /// inline-HTML characters would equally corrupt a display name.
    /// </summary>
    private static readonly SearchValues<char> MarkdownMetacharacters = SearchValues.Create(@"\`*_[]<>~");

    /// <summary>Appends a contributor as either an <c>@mention</c> or escaped literal text.</summary>
    /// <param name="builder">The builder to append to.</param>
    /// <param name="author">The resolved contributor identity.</param>
    internal static void Append(StringBuilder builder, ContributorIdentity author)
    {
        ArgumentNullException.ThrowIfNull(author);

        if (author.IsLogin && IsMentionableLogin(author.Value))
        {
            _ = builder.Append('@').Append(author.Value);
            return;
        }

        AppendEscaped(builder, author.Value);
    }

    /// <summary>
    /// Determines whether an identifier is a syntactically valid GitHub login, and therefore safe
    /// to render as an <c>@mention</c>. A login is 1-39 characters of ASCII alphanumerics and
    /// single hyphens, and may neither begin nor end with a hyphen.
    /// </summary>
    /// <param name="author">The identifier to inspect.</param>
    /// <returns>True when the identifier could name a GitHub account; otherwise, false.</returns>
    internal static bool IsMentionableLogin(string? author)
    {
        if (string.IsNullOrEmpty(author) || author.Length > MaxLoginLength)
        {
            return false;
        }

        if (author[0] == '-' || author[^1] == '-')
        {
            return false;
        }

        var previousWasHyphen = false;
        foreach (var character in author)
        {
            if (character == '-')
            {
                if (previousWasHyphen)
                {
                    return false;
                }

                previousWasHyphen = true;
                continue;
            }

            if (!char.IsAsciiLetterOrDigit(character))
            {
                return false;
            }

            previousWasHyphen = false;
        }

        return true;
    }

    /// <summary>Appends text with its Markdown metacharacters backslash-escaped.</summary>
    /// <param name="builder">The builder to append to.</param>
    /// <param name="value">The literal text to append.</param>
    private static void AppendEscaped(StringBuilder builder, string value)
    {
        foreach (var character in value)
        {
            if (MarkdownMetacharacters.Contains(character))
            {
                _ = builder.Append('\\');
            }

            _ = builder.Append(character);
        }
    }
}
