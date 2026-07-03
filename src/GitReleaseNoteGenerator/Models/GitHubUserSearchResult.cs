// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace GitReleaseNoteGenerator.Models;

/// <summary>
/// The result of the GitHub "search users" API, carrying the matched accounts.
/// </summary>
/// <param name="Items">The matched GitHub accounts, most relevant first, or null.</param>
public sealed record GitHubUserSearchResult(IReadOnlyList<GitHubUser>? Items);
