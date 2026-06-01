// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.CommandLine;

namespace GitReleaseNoteGenerator.Commands;

/// <summary>
/// Contains the options accepted by the generate command.
/// </summary>
/// <param name="TokenOption">The option for the GitHub token.</param>
/// <param name="OwnerOption">The option for the repository owner.</param>
/// <param name="RepoOption">The option for the repository name.</param>
/// <param name="BaseRefOption">The option for the base comparison ref.</param>
/// <param name="HeadRefOption">The option for the head comparison ref.</param>
/// <param name="VersionOption">The option for the release version.</param>
/// <param name="OutputFileOption">The option for the output file.</param>
/// <param name="GitHubOutputOption">The option for writing to GitHub Actions output.</param>
/// <param name="OutputNameOption">The option for the GitHub Actions output name.</param>
internal sealed record GenerateCommandOptions(
    Option<string?> TokenOption,
    Option<string?> OwnerOption,
    Option<string?> RepoOption,
    Option<string?> BaseRefOption,
    Option<string?> HeadRefOption,
    Option<string?> VersionOption,
    Option<FileInfo?> OutputFileOption,
    Option<bool> GitHubOutputOption,
    Option<string> OutputNameOption);
