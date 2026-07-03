// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace GitReleaseNoteGenerator.Models;

/// <summary>
/// The git-level detail of a commit: its message and author/committer signatures. This is the
/// "commit" object nested inside a GitHub commit response.
/// </summary>
/// <param name="Message">The full commit message, or null.</param>
/// <param name="Author">The git author signature, or null.</param>
/// <param name="Committer">The git committer signature, or null.</param>
public sealed record GitCommitDetail(string? Message, GitSignature? Author, GitSignature? Committer);
