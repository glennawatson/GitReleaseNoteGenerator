// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace GitReleaseNoteGenerator.Commands;

/// <summary>
/// Contains raw command values before validation and inferred value resolution.
/// </summary>
/// <param name="Token">The GitHub token.</param>
/// <param name="Owner">The repository owner.</param>
/// <param name="Repo">The repository name.</param>
/// <param name="BaseRef">The base comparison ref.</param>
/// <param name="HeadRef">The head comparison ref.</param>
/// <param name="Version">The release version.</param>
/// <param name="OutputFile">The file to write release notes to.</param>
/// <param name="GitHubOutput">A value indicating whether to write to GitHub Actions output.</param>
/// <param name="OutputName">The GitHub Actions output name.</param>
internal sealed record GenerateCommandValues(
    string? Token,
    string? Owner,
    string? Repo,
    string? BaseRef,
    string? HeadRef,
    string? Version,
    FileInfo? OutputFile,
    bool GitHubOutput,
    string OutputName);
