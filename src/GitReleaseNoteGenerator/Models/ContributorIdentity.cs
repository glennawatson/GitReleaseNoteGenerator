// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace GitReleaseNoteGenerator.Models;

/// <summary>
/// A contributor after resolution: the identifier to attribute them by, and whether that
/// identifier is a GitHub login or merely the display name recorded in the commit.
/// </summary>
/// <remarks>
/// The distinction cannot be recovered from the text alone. A display name such as "Lukas" is
/// indistinguishable in shape from a login, so attributing it as <c>@Lukas</c> silently credits
/// whichever unrelated account happens to hold that name. Only the resolution step knows which of
/// the two it produced, so it records the answer here rather than leaving the renderer to guess.
/// </remarks>
/// <param name="Value">The identifier to attribute the contributor by.</param>
/// <param name="IsLogin">True when <paramref name="Value"/> is a GitHub login; false when it is a display name.</param>
public sealed record ContributorIdentity(string Value, bool IsLogin)
{
    /// <summary>
    /// Gets a comparer that orders and de-duplicates identities by their value, ignoring case and
    /// provenance, so the same person collected once as a login and once as a display name
    /// collapses to a single entry.
    /// </summary>
    public static IComparer<ContributorIdentity> ValueComparer { get; } = new ByValueComparer();

    /// <summary>Creates an identity for a confirmed GitHub login.</summary>
    /// <param name="login">The GitHub login.</param>
    /// <returns>An identity marked as a login.</returns>
    public static ContributorIdentity ForLogin(string login) => new(login, IsLogin: true);

    /// <summary>Creates an identity for a display name that could not be resolved to a login.</summary>
    /// <param name="name">The contributor's display name.</param>
    /// <returns>An identity marked as a display name.</returns>
    public static ContributorIdentity ForDisplayName(string name) => new(name, IsLogin: false);

    /// <summary>Compares identities by value alone, ignoring case and provenance.</summary>
    private sealed class ByValueComparer : IComparer<ContributorIdentity>
    {
        /// <inheritdoc/>
        public int Compare(ContributorIdentity? x, ContributorIdentity? y) =>
            string.Compare(x?.Value, y?.Value, StringComparison.OrdinalIgnoreCase);
    }
}
