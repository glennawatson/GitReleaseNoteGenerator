// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace GitReleaseNoteGenerator.Models;

/// <summary>One page of the GitHub "compare two commits" API, carrying the commits between the refs.</summary>
/// <param name="TotalCommits">The number of commits in the whole comparison range, across every page.</param>
/// <param name="Commits">The commits on this page of the comparison range, or null.</param>
[System.Diagnostics.DebuggerDisplay("GitHubComparison: {ToString(),nq}")]
public sealed record GitHubComparison(int TotalCommits, IReadOnlyList<GitHubCommit>? Commits);
