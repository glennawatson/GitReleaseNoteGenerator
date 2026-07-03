// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace GitReleaseNoteGenerator.Models;

/// <summary>The git-level author or committer signature recorded in a commit's metadata.</summary>
/// <param name="Name">The display name from the git signature, or null.</param>
/// <param name="Email">The email address from the git signature, or null.</param>
public sealed record GitSignature(string? Name, string? Email);
