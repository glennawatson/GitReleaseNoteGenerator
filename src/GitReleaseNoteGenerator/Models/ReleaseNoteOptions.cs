// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace GitReleaseNoteGenerator.Models;

/// <summary>
/// Options for release note generation, populated from CLI arguments and environment variables.
/// </summary>
public sealed record ReleaseNoteOptions
{
    /// <summary>
    /// Gets the GitHub personal access token.
    /// </summary>
    public required string Token { get; init; }

    /// <summary>
    /// Gets the repository owner (organization or user).
    /// </summary>
    public required string Owner { get; init; }

    /// <summary>
    /// Gets the repository name.
    /// </summary>
    public required string Repo { get; init; }

    /// <summary>
    /// Gets the base ref to compare from (e.g., a tag). If null, uses the latest release tag.
    /// </summary>
    public string? BaseRef { get; init; }

    /// <summary>
    /// Gets the head ref to compare to. If null, uses the default branch.
    /// </summary>
    public string? HeadRef { get; init; }

    /// <summary>
    /// Gets the version string to use in the release notes. If null, auto-detects via NBGV.
    /// </summary>
    public string? Version { get; init; }

    /// <summary>
    /// Gets the output file path. If null, does not write to file.
    /// </summary>
    public FileInfo? OutputFile { get; init; }

    /// <summary>
    /// Gets a value indicating whether to write to GITHUB_OUTPUT.
    /// </summary>
    public bool GitHubOutput { get; init; }

    /// <summary>
    /// Gets the variable name for GITHUB_OUTPUT.
    /// </summary>
    public string OutputName { get; init; } = "changelog";
}
