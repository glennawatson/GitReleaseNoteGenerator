// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.CommandLine;

using GitReleaseNoteGenerator.Infrastructure;
using GitReleaseNoteGenerator.Services;

using Microsoft.Extensions.Logging;

namespace GitReleaseNoteGenerator.Commands;

/// <summary>
/// Wires the generate root command to its action. Decision logic lives in
/// <see cref="CommandOptionsFactory"/> and <see cref="CommandArgumentResolver"/>; this type
/// owns only the console/process side effects of execution.
/// </summary>
internal static partial class GenerateCommand
{
    /// <summary>
    /// Creates the root command with all options and its execution action.
    /// </summary>
    /// <returns>The configured root command.</returns>
    public static RootCommand Create()
    {
        var options = CommandOptionsFactory.CreateOptions();
        var rootCommand = CommandOptionsFactory.CreateRootCommand(options);

        rootCommand.SetAction(async (parseResult, _) => await ExecuteAsync(parseResult, options).ConfigureAwait(false));

        return rootCommand;
    }

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

            var values = CommandArgumentResolver.ReadValues(parseResult, options);
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
        var status = CommandArgumentResolver.Validate(values);
        if (status != CommandValidationStatus.Valid)
        {
            await ReportValidationFailureAsync(status, logger).ConfigureAwait(false);
            return null;
        }

        var version = await ResolveVersionAsync(values.Version, logger).ConfigureAwait(false);

        if (string.IsNullOrEmpty(version))
        {
            return null;
        }

        return CommandArgumentResolver.CreateArguments(values, version);
    }

    /// <summary>
    /// Reports a validation failure to the log and standard error and sets the process exit code.
    /// </summary>
    /// <param name="status">The validation failure status.</param>
    /// <param name="logger">The logger instance.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private static async Task ReportValidationFailureAsync(CommandValidationStatus status, ILogger logger)
    {
        if (status == CommandValidationStatus.TokenMissing)
        {
            LogTokenRequired(logger);
            await Console.Error.WriteLineAsync("Error: GitHub token is required. Use --token or set GITHUB_TOKEN environment variable.").ConfigureAwait(false);
        }
        else
        {
            LogRepoRequired(logger);
            await Console.Error.WriteLineAsync("Error: Repository owner and name are required. Use --owner/--repo or set GITHUB_REPOSITORY environment variable.").ConfigureAwait(false);
        }

        Environment.ExitCode = 1;
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
}
