// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Octokit;

namespace GitReleaseNoteGenerator.Models;

/// <summary>
/// A commit that has been assigned to a category with extracted author information.
/// </summary>
/// <param name="Commit">The GitHub commit.</param>
/// <param name="Category">The category name this commit was assigned to.</param>
/// <param name="Priority">The display priority of the category.</param>
/// <param name="Authors">The normalized author identifiers for this commit.</param>
public sealed record CategorizedCommit(GitHubCommit Commit, string Category, int Priority, SortedSet<string> Authors);
