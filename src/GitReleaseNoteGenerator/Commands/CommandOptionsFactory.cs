// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.CommandLine;

namespace GitReleaseNoteGenerator.Commands;

/// <summary>Builds the <see cref="System.CommandLine"/> options and root command for the generate command.</summary>
internal static class CommandOptionsFactory
{
    /// <summary>The default GitHub Actions output variable name.</summary>
    internal const string DefaultOutputName = "changelog";

    /// <summary>Creates the options accepted by the generate command.</summary>
    /// <returns>The configured options.</returns>
    public static GenerateCommandOptions CreateOptions()
    {
        var tokenOption = new Option<string?>("--token")
        {
            Description = "GitHub personal access token (defaults to GITHUB_TOKEN env var)",
        };

        var ownerOption = new Option<string?>("--owner")
        {
            Description = "Repository owner (defaults to GITHUB_REPOSITORY env var)",
        };

        var repoOption = new Option<string?>("--repo")
        {
            Description = "Repository name (defaults to GITHUB_REPOSITORY env var)",
        };

        var baseRefOption = new Option<string?>("--base-ref")
        {
            Description = "Base ref to compare from (defaults to latest release tag)",
        };

        var headRefOption = new Option<string?>("--head-ref")
        {
            Description = "Head ref to compare to (defaults to default branch)",
        };

        var versionOption = new Option<string?>("--release-version")
        {
            Description = "Version string for the release notes heading (required)",
        };

        var outputFileOption = new Option<FileInfo?>("--output-file")
        {
            Description = "Write release notes to a file",
        };

        var githubOutputOption = new Option<bool>("--github-output")
        {
            Description = "Write release notes to GITHUB_OUTPUT",
            DefaultValueFactory = _ => false,
        };

        var outputNameOption = new Option<string>("--output-name")
        {
            Description = "Variable name when writing to GITHUB_OUTPUT",
            DefaultValueFactory = _ => DefaultOutputName,
        };

        return new(
            tokenOption,
            ownerOption,
            repoOption,
            baseRefOption,
            headRefOption,
            versionOption,
            outputFileOption,
            githubOutputOption,
            outputNameOption);
    }

    /// <summary>Creates the root command populated with the supplied options.</summary>
    /// <param name="options">The options to attach to the command.</param>
    /// <returns>The configured root command.</returns>
    public static RootCommand CreateRootCommand(GenerateCommandOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new RootCommand("Generate categorized release notes from git commit history")
        {
            options.TokenOption,
            options.OwnerOption,
            options.RepoOption,
            options.BaseRefOption,
            options.HeadRefOption,
            options.VersionOption,
            options.OutputFileOption,
            options.GitHubOutputOption,
            options.OutputNameOption,
        };
    }
}
