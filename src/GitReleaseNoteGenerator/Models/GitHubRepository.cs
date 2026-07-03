// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace GitReleaseNoteGenerator.Models;

/// <summary>
/// The subset of a GitHub repository payload used to resolve the default comparison head.
/// </summary>
/// <param name="DefaultBranch">The repository's default branch name, or null.</param>
public sealed record GitHubRepository(string? DefaultBranch);
