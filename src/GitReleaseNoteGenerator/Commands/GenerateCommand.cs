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
        var commandConfiguration = CreateRootCommandAndOptions();

        SetRootCommandAction(commandConfiguration.RootCommand, commandConfiguration.Options);

        return commandConfiguration.RootCommand;
    }

    /// <summary>
    /// Creates the root command and all options.
    /// </summary>
    /// <returns>The root command configuration.</returns>
    private static GenerateRootCommandConfiguration CreateRootCommandAndOptions()
    {
        var options = CreateOptions();

        var rootCommand = new RootCommand("Generate categorized release notes from git commit history")
        {
            options.TokenOption,
            options.OwnerOption,
            options.RepoOption,
            options.BaseRefOption,
            options.HeadRefOption,
            options.VersionOption,
            options.OutputFileOption,
            options.GitHubOutputOption,
            options.OutputNameOption
        };

        return new(options, rootCommand);
    }

    /// <summary>
    /// Creates the options for the root command.
    /// </summary>
    /// <returns>The options.</returns>
    private static GenerateCommandOptions CreateOptions()
    {
        var tokenOption = new Option<string?>("--token")
        {
            Description = "GitHub personal access token (defaults to GITHUB_TOKEN env var)"
        };

        var ownerOption = new Option<string?>("--owner")
        {
            Description = "Repository owner (defaults to GITHUB_REPOSITORY env var)"
        };

        var repoOption = new Option<string?>("--repo")
        {
            Description = "Repository name (defaults to GITHUB_REPOSITORY env var)"
        };

        var baseRefOption = new Option<string?>("--base-ref")
        {
            Description = "Base ref to compare from (defaults to latest release tag)"
        };

        var headRefOption = new Option<string?>("--head-ref")
        {
            Description = "Head ref to compare to (defaults to default branch)"
        };

        var versionOption = new Option<string?>("--release-version")
        {
            Description = "Version string for release notes (defaults to NBGV auto-detection)"
        };

        var outputFileOption = new Option<FileInfo?>("--output-file")
        {
            Description = "Write release notes to a file"
        };

        var githubOutputOption = new Option<bool>("--github-output")
        {
            Description = "Write release notes to GITHUB_OUTPUT",
            DefaultValueFactory = _ => false
        };

        var outputNameOption = new Option<string>("--output-name")
        {
            Description = "Variable name when writing to GITHUB_OUTPUT",
            DefaultValueFactory = _ => "changelog"
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

    /// <summary>
    /// Configures the root command action.
    /// </summary>
    /// <param name="rootCommand">The root command to configure.</param>
    /// <param name="options">The options used to parse command arguments.</param>
    private static void SetRootCommandAction(RootCommand rootCommand, GenerateCommandOptions options) =>
        rootCommand.SetAction(async (parseResult, _) => await ExecuteAsync(parseResult, options).ConfigureAwait(false));

    /// <summary>
    /// Executes release note generation for parsed command-line input.
    /// </summary>
    /// <param name="parseResult">The parse result from the command-line invocation.</param>
    /// <param name="options">The configured command options.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private static async Task ExecuteAsync(ParseResult parseResult, GenerateCommandOptions options)
    {
        using var loggerFactory = CreateLoggerFactory();
        var logger = loggerFactory.CreateLogger("GitReleaseNoteGenerator");

        try
        {
            Console.WriteLine("Starting release note generation...");

            var values = GetCommandValues(parseResult, options);
            WriteCommandSummary(values);

            var arguments = await ValidateAndResolveArgumentsAsync(values, logger).ConfigureAwait(false);

            if (arguments is null)
            {
                return;
            }

            var releaseNotes = await GenerateReleaseNotesAsync(arguments, logger).ConfigureAwait(false);

            await WriteReleaseNotesAsync(releaseNotes, arguments, logger).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogUnhandledError(logger, ex);
            await Console.Error.WriteLineAsync($"Error: {ex.Message}").ConfigureAwait(false);
            Environment.ExitCode = 1;
        }
    }

    /// <summary>
    /// Creates the logger factory used by command execution.
    /// </summary>
    /// <returns>The configured logger factory.</returns>
    private static ILoggerFactory CreateLoggerFactory() =>
        LoggerFactory.Create(builder =>
            builder.AddConsole().SetMinimumLevel(LogLevel.Information));

    /// <summary>
    /// Gets raw command values from the parse result and GitHub Actions environment.
    /// </summary>
    /// <param name="parseResult">The parse result from the command-line invocation.</param>
    /// <param name="options">The configured command options.</param>
    /// <returns>The raw command values.</returns>
    private static GenerateCommandValues GetCommandValues(ParseResult parseResult, GenerateCommandOptions options) =>
        new(
            parseResult.GetValue(options.TokenOption) ?? GitHubActionEnvironment.Token,
            parseResult.GetValue(options.OwnerOption) ?? GitHubActionEnvironment.RepositoryOwner,
            parseResult.GetValue(options.RepoOption) ?? GitHubActionEnvironment.RepositoryName,
            parseResult.GetValue(options.BaseRefOption),
            parseResult.GetValue(options.HeadRefOption),
            parseResult.GetValue(options.VersionOption),
            parseResult.GetValue(options.OutputFileOption),
            parseResult.GetValue(options.GitHubOutputOption),
            parseResult.GetValue(options.OutputNameOption) ?? "changelog");

    /// <summary>
    /// Writes a summary of the command values to the console.
    /// </summary>
    /// <param name="values">The command values to summarize.</param>
    private static void WriteCommandSummary(GenerateCommandValues values)
    {
        Console.WriteLine($"Token present: {!string.IsNullOrEmpty(values.Token)}");
        Console.WriteLine($"Owner: {values.Owner ?? "(not set)"}");
        Console.WriteLine($"Repo: {values.Repo ?? "(not set)"}");
        Console.WriteLine($"Version: {values.Version ?? "(auto-detect)"}");
        Console.WriteLine($"Output file: {values.OutputFile?.FullName ?? "(none)"}");
    }

    /// <summary>
    /// Validates required command values and resolves values that can be inferred.
    /// </summary>
    /// <param name="values">The raw command values.</param>
    /// <param name="logger">The logger instance.</param>
    /// <returns>The resolved command arguments, or <see langword="null"/> when validation fails.</returns>
    private static async Task<GenerateCommandArguments?> ValidateAndResolveArgumentsAsync(GenerateCommandValues values, ILogger logger)
    {
        if (!await ValidateRequiredValuesAsync(values, logger).ConfigureAwait(false))
        {
            return null;
        }

        var version = await ResolveVersionAsync(values.Version, logger).ConfigureAwait(false);

        if (string.IsNullOrEmpty(version))
        {
            return null;
        }

        return new(
            values.Token!,
            values.Owner!,
            values.Repo!,
            values.BaseRef,
            values.HeadRef,
            version,
            values.OutputFile,
            values.GitHubOutput,
            values.OutputName);
    }

    /// <summary>
    /// Validates command values that must be provided by arguments or environment variables.
    /// </summary>
    /// <param name="values">The command values to validate.</param>
    /// <param name="logger">The logger instance.</param>
    /// <returns><see langword="true"/> when required values are present; otherwise, <see langword="false"/>.</returns>
    private static async Task<bool> ValidateRequiredValuesAsync(GenerateCommandValues values, ILogger logger)
    {
        if (string.IsNullOrEmpty(values.Token))
        {
            LogTokenRequired(logger);
            await Console.Error.WriteLineAsync("Error: GitHub token is required. Use --token or set GITHUB_TOKEN environment variable.").ConfigureAwait(false);
            Environment.ExitCode = 1;
            return false;
        }

        if (!string.IsNullOrEmpty(values.Owner) && !string.IsNullOrEmpty(values.Repo))
        {
            return true;
        }

        LogRepoRequired(logger);
        await Console.Error.WriteLineAsync("Error: Repository owner and name are required. Use --owner/--repo or set GITHUB_REPOSITORY environment variable.").ConfigureAwait(false);
        Environment.ExitCode = 1;
        return false;
    }

    /// <summary>
    /// Resolves the release version from the command value or auto-detection.
    /// </summary>
    /// <param name="version">The version supplied by the command, if any.</param>
    /// <param name="logger">The logger instance.</param>
    /// <returns>The resolved version, or <see langword="null"/> when no version can be resolved.</returns>
    private static async Task<string?> ResolveVersionAsync(string? version, ILogger logger)
    {
        if (!string.IsNullOrEmpty(version))
        {
            return version;
        }

        LogDetectingVersion(logger);
        version = await VersionDetector.DetectVersionAsync(Directory.GetCurrentDirectory(), logger).ConfigureAwait(false);

        if (string.IsNullOrEmpty(version))
        {
            LogVersionDetectionFailed(logger);
            await Console.Error.WriteLineAsync("Error: Could not auto-detect version. Specify --release-version explicitly or install NBGV.").ConfigureAwait(false);
            Environment.ExitCode = 1;
            return null;
        }

        LogDetectedVersion(logger, version);
        return version;
    }

    /// <summary>
    /// Generates release notes using the resolved command arguments.
    /// </summary>
    /// <param name="arguments">The resolved command arguments.</param>
    /// <param name="logger">The logger instance.</param>
    /// <returns>The generated release notes.</returns>
    private static async Task<string> GenerateReleaseNotesAsync(GenerateCommandArguments arguments, ILogger logger)
    {
        Console.WriteLine($"Generating release notes for {arguments.Owner}/{arguments.Repo} version {arguments.Version}...");

        var client = GitHubClientFactory.Create(arguments.Token);
        var generator = new ReleaseNoteGenerator(client, logger);

        var releaseNotes = await generator.GenerateAsync(arguments.Owner, arguments.Repo, arguments.Version, arguments.BaseRef, arguments.HeadRef).ConfigureAwait(false);

        Console.WriteLine($"Release notes generated ({releaseNotes.Length} characters)");

        return releaseNotes;
    }

    /// <summary>
    /// Writes generated release notes to the configured outputs.
    /// </summary>
    /// <param name="releaseNotes">The generated release notes.</param>
    /// <param name="arguments">The resolved command arguments.</param>
    /// <param name="logger">The logger instance.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private static async Task WriteReleaseNotesAsync(string releaseNotes, GenerateCommandArguments arguments, ILogger logger)
    {
        OutputWriter.WriteToStdout(releaseNotes);

        if (arguments.OutputFile is not null)
        {
            await OutputWriter.WriteToFileAsync(releaseNotes, arguments.OutputFile, logger).ConfigureAwait(false);
            Console.WriteLine($"Written to {arguments.OutputFile.FullName}");
        }

        if (!arguments.GitHubOutput)
        {
            return;
        }

        await OutputWriter.WriteToGitHubOutputAsync(releaseNotes, arguments.OutputName, logger)
            .ConfigureAwait(false);
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

    /// <summary>
    /// Contains the root command and the options used by its action.
    /// </summary>
    /// <param name="Options">The configured command options.</param>
    /// <param name="RootCommand">The configured root command.</param>
    private sealed record GenerateRootCommandConfiguration(GenerateCommandOptions Options, RootCommand RootCommand);

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
    private sealed record GenerateCommandOptions(
        Option<string?> TokenOption,
        Option<string?> OwnerOption,
        Option<string?> RepoOption,
        Option<string?> BaseRefOption,
        Option<string?> HeadRefOption,
        Option<string?> VersionOption,
        Option<FileInfo?> OutputFileOption,
        Option<bool> GitHubOutputOption,
        Option<string> OutputNameOption);

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
    private sealed record GenerateCommandValues(
        string? Token,
        string? Owner,
        string? Repo,
        string? BaseRef,
        string? HeadRef,
        string? Version,
        FileInfo? OutputFile,
        bool GitHubOutput,
        string OutputName);

    /// <summary>
    /// Contains validated and resolved command arguments.
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
    private sealed record GenerateCommandArguments(
        string Token,
        string Owner,
        string Repo,
        string? BaseRef,
        string? HeadRef,
        string Version,
        FileInfo? OutputFile,
        bool GitHubOutput,
        string OutputName);
}
