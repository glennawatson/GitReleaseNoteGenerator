// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace GitReleaseNoteGenerator.Models;

/// <summary>The result of the GitHub "compare two commits" API, carrying the commits between the refs.</summary>
/// <param name="Commits">The commits contained in the comparison range, or null.</param>
public sealed record GitHubComparison(IReadOnlyList<GitHubCommit>? Commits);
