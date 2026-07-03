// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace GitReleaseNoteGenerator.Models;

/// <summary>
/// A commit as returned by the GitHub REST API, combining the git-level detail with the
/// GitHub accounts the API attributed to the author and committer.
/// </summary>
/// <param name="Sha">The commit SHA, or null.</param>
/// <param name="Commit">The git-level commit detail (message and signatures).</param>
/// <param name="Author">The GitHub account attributed to the author, or null.</param>
/// <param name="Committer">The GitHub account attributed to the committer, or null.</param>
public sealed record GitHubCommit(string? Sha, GitCommitDetail Commit, GitHubUser? Author, GitHubUser? Committer);
