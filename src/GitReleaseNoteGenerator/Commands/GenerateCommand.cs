// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.CommandLine;

using GitReleaseNoteGenerator.Infrastructure;
using GitReleaseNoteGenerator.Services;

using Microsoft.Extensions.Logging;

namespace GitReleaseNoteGenerator.Commands;

/// <summary>
/// Defines the root command for generating release notes.
/// </summary>
internal static partial class GenerateCommand
{
    /// <summary>
    /// Creates the root command with all options.
    /// </summary>
    /// <returns>The configured root command.</returns>
    public static RootCommand Create()
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
            Description = "Version string for release notes (defaults to NBGV auto-detection)",
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
            DefaultValueFactory = _ => "changelog",
        };

        var rootCommand = new RootCommand("Generate categorized release notes from git commit history")
        {
            tokenOption,
            ownerOption,
            repoOption,
            baseRefOption,
            headRefOption,
            versionOption,
            outputFileOption,
            githubOutputOption,
            outputNameOption,
        };

        rootCommand.SetAction(async (parseResult, cancellationToken) =>
        {
            using var loggerFactory = LoggerFactory.Create(builder =>
                builder.AddConsole().SetMinimumLevel(LogLevel.Information));
            var logger = loggerFactory.CreateLogger("GitReleaseNoteGenerator");

            try
            {
                Console.WriteLine("Starting release note generation...");

                var token = parseResult.GetValue(tokenOption) ?? GitHubActionEnvironment.Token;
                var owner = parseResult.GetValue(ownerOption) ?? GitHubActionEnvironment.RepositoryOwner;
                var repo = parseResult.GetValue(repoOption) ?? GitHubActionEnvironment.RepositoryName;
                var baseRef = parseResult.GetValue(baseRefOption);
                var headRef = parseResult.GetValue(headRefOption);
                var version = parseResult.GetValue(versionOption);
                var outputFile = parseResult.GetValue(outputFileOption);
                var githubOutput = parseResult.GetValue(githubOutputOption);
                var outputName = parseResult.GetValue(outputNameOption) ?? "changelog";

                Console.WriteLine($"Token present: {!string.IsNullOrEmpty(token)}");
                Console.WriteLine($"Owner: {owner ?? "(not set)"}");
                Console.WriteLine($"Repo: {repo ?? "(not set)"}");
                Console.WriteLine($"Version: {version ?? "(auto-detect)"}");
                Console.WriteLine($"Output file: {outputFile?.FullName ?? "(none)"}");

                if (string.IsNullOrEmpty(token))
                {
                    LogTokenRequired(logger);
                    await Console.Error.WriteLineAsync("Error: GitHub token is required. Use --token or set GITHUB_TOKEN environment variable.").ConfigureAwait(false);
                    Environment.ExitCode = 1;
                    return;
                }

                if (string.IsNullOrEmpty(owner) || string.IsNullOrEmpty(repo))
                {
                    LogRepoRequired(logger);
                    await Console.Error.WriteLineAsync("Error: Repository owner and name are required. Use --owner/--repo or set GITHUB_REPOSITORY environment variable.").ConfigureAwait(false);
                    Environment.ExitCode = 1;
                    return;
                }

                if (string.IsNullOrEmpty(version))
                {
                    LogDetectingVersion(logger);
                    version = await VersionDetector.DetectVersionAsync(Directory.GetCurrentDirectory(), logger).ConfigureAwait(false);

                    if (string.IsNullOrEmpty(version))
                    {
                        LogVersionDetectionFailed(logger);
                        await Console.Error.WriteLineAsync("Error: Could not auto-detect version. Specify --release-version explicitly or install NBGV.").ConfigureAwait(false);
                        Environment.ExitCode = 1;
                        return;
                    }

                    LogDetectedVersion(logger, version);
                }

                Console.WriteLine($"Generating release notes for {owner}/{repo} version {version}...");

                var client = GitHubClientFactory.Create(token);
                var generator = new ReleaseNoteGenerator(client, logger);

                var releaseNotes = await generator.GenerateAsync(owner, repo, version, baseRef, headRef).ConfigureAwait(false);

                Console.WriteLine($"Release notes generated ({releaseNotes.Length} characters)");

                OutputWriter.WriteToStdout(releaseNotes);

                if (outputFile is not null)
                {
                    await OutputWriter.WriteToFileAsync(releaseNotes, outputFile, logger).ConfigureAwait(false);
                    Console.WriteLine($"Written to {outputFile.FullName}");
                }

                if (githubOutput)
                {
                    await OutputWriter.WriteToGitHubOutputAsync(releaseNotes, outputName, logger).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                LogUnhandledError(logger, ex);
                await Console.Error.WriteLineAsync($"Error: {ex.Message}").ConfigureAwait(false);
                Environment.ExitCode = 1;
            }
        });

        return rootCommand;
    }

    /// <summary>
    /// Logs that the GitHub token was not provided.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    [LoggerMessage(Level = LogLevel.Error, Message = "GitHub token is required. Use --token or set GITHUB_TOKEN environment variable")]
    private static partial void LogTokenRequired(ILogger logger);

    /// <summary>
    /// Logs that the repository owner and name were not provided.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    [LoggerMessage(Level = LogLevel.Error, Message = "Repository owner and name are required. Use --owner/--repo or set GITHUB_REPOSITORY environment variable")]
    private static partial void LogRepoRequired(ILogger logger);

    /// <summary>
    /// Logs that version auto-detection is being attempted via NBGV.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "No --version specified, detecting via NBGV...")]
    private static partial void LogDetectingVersion(ILogger logger);

    /// <summary>
    /// Logs that version auto-detection failed.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    [LoggerMessage(Level = LogLevel.Error, Message = "Could not auto-detect version. Specify --release-version explicitly or install NBGV")]
    private static partial void LogVersionDetectionFailed(ILogger logger);

    /// <summary>
    /// Logs the version that was auto-detected.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="version">The detected version string.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Detected version: {Version}")]
    private static partial void LogDetectedVersion(ILogger logger, string version);

    /// <summary>
    /// Logs an unhandled error during release note generation.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="exception">The exception that occurred.</param>
    [LoggerMessage(Level = LogLevel.Error, Message = "Release note generation failed")]
    private static partial void LogUnhandledError(ILogger logger, Exception exception);
}
