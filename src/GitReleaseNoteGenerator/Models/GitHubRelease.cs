// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace GitReleaseNoteGenerator.Models;

/// <summary>
/// The subset of a GitHub release payload used to resolve the base ref for comparison.
/// </summary>
/// <param name="TagName">The tag name the release points at, or null.</param>
public sealed record GitHubRelease(string? TagName);
