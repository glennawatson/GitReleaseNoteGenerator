// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace GitReleaseNoteGenerator.Models;

/// <summary>
/// Represents a single contributor candidate extracted from a commit, before resolution
/// to a canonical GitHub login. A contributor may originate from the GitHub-resolved author,
/// the raw git author/committer metadata, or a "Co-authored-by:" trailer.
/// </summary>
/// <param name="Login">The GitHub login if already resolved by the API, or null.</param>
/// <param name="Name">The display name (git author name or trailer name), or null.</param>
/// <param name="Email">The associated email address, or null.</param>
public sealed record CommitContributor(string? Login, string? Name, string? Email);
