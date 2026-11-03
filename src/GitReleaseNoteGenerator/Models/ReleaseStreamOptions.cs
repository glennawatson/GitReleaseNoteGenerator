// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using GitReleaseNoteGenerator.Services;

namespace GitReleaseNoteGenerator.Models;

/// <summary>Selects a release stream and the new tag used in changelog links.</summary>
/// <param name="TagPrefix">An exact tag prefix, or null to use the repository's latest release.</param>
/// <param name="ReleaseTag">The exact new release tag, or null to infer it from the prefix and version.</param>
[System.Diagnostics.DebuggerDisplay("ReleaseStreamOptions: {ToString(),nq}")]
public sealed record ReleaseStreamOptions(string? TagPrefix = null, string? ReleaseTag = null)
{
    /// <summary>Resolves the new tag while preserving the default version-prefix inference.</summary>
    /// <param name="version">The release version supplied by the caller.</param>
    /// <param name="baseRef">The resolved base ref, or null before discovery.</param>
    /// <returns>The new tag used in changelog links.</returns>
    internal string ResolveReleaseTag(string version, string? baseRef)
    {
        if (!string.IsNullOrWhiteSpace(ReleaseTag))
        {
            return ReleaseTag;
        }

        if (string.IsNullOrEmpty(TagPrefix))
        {
            return ReleaseNoteGenerator.AlignVersionWithBaseRefPrefix(version, baseRef);
        }

        return version.StartsWith(TagPrefix, StringComparison.Ordinal) ? version : string.Concat(TagPrefix, version);
    }
}
