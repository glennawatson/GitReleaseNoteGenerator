// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace GitReleaseNoteGenerator.Models;

/// <summary>The subset of a GitHub release payload used to resolve the base ref for comparison.</summary>
/// <param name="TagName">The tag name the release points at, or null.</param>
[System.Diagnostics.DebuggerDisplay("GitHubRelease: {ToString(),nq}")]
public sealed record GitHubRelease(string? TagName)
{
    /// <summary>Gets a value indicating whether the release is an unpublished draft.</summary>
    public bool Draft { get; init; }

    /// <summary>Gets a value indicating whether the release is a pre-release.</summary>
    public bool Prerelease { get; init; }

    /// <summary>Gets the publication time, or null when the release has not been published.</summary>
    public DateTimeOffset? PublishedAt { get; init; }
}
