// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging;

namespace GitReleaseNoteGenerator.Infrastructure;

/// <summary>
/// Writes release notes to various outputs: stdout, file, and GITHUB_OUTPUT.
/// </summary>
public static partial class OutputWriter
{
    /// <summary>
    /// Writes the release notes to stdout.
    /// </summary>
    /// <param name="releaseNotes">The release notes content.</param>
    public static void WriteToStdout(string releaseNotes) =>
        Console.WriteLine(releaseNotes);

    /// <summary>
    /// Writes the release notes to a file.
    /// </summary>
    /// <param name="releaseNotes">The release notes content.</param>
    /// <param name="outputFile">The file to write to.</param>
    /// <param name="logger">Logger for status messages.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public static async Task WriteToFileAsync(string releaseNotes, FileInfo outputFile, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(outputFile);

        await File.WriteAllTextAsync(outputFile.FullName, releaseNotes).ConfigureAwait(false);
        LogFileWritten(logger, outputFile.FullName);
    }

    /// <summary>
    /// Writes the release notes to the GITHUB_OUTPUT file using heredoc delimiter format.
    /// Uses a unique GUID delimiter to prevent content collisions.
    /// </summary>
    /// <param name="releaseNotes">The release notes content.</param>
    /// <param name="outputName">The variable name for the output.</param>
    /// <param name="logger">Logger for status messages.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public static async Task WriteToGitHubOutputAsync(string releaseNotes, string outputName, ILogger logger)
    {
        var githubOutputPath = GitHubActionEnvironment.OutputFile;
        if (string.IsNullOrEmpty(githubOutputPath))
        {
            LogGitHubOutputNotSet(logger);
            return;
        }

        var delimiter = $"ghadelimiter_{Guid.NewGuid():N}";
        var content = $"{outputName}<<{delimiter}{Environment.NewLine}{releaseNotes}{Environment.NewLine}{delimiter}{Environment.NewLine}";

        await File.AppendAllTextAsync(githubOutputPath, content).ConfigureAwait(false);
        LogGitHubOutputWritten(logger, outputName);
    }

    /// <summary>
    /// Logs that release notes were written to a file.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="filePath">The path of the file that was written.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Release notes written to {FilePath}")]
    private static partial void LogFileWritten(ILogger logger, string filePath);

    /// <summary>
    /// Logs that the GITHUB_OUTPUT environment variable is not set.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "GITHUB_OUTPUT environment variable is not set, skipping GitHub output")]
    private static partial void LogGitHubOutputNotSet(ILogger logger);

    /// <summary>
    /// Logs that release notes were written to the GITHUB_OUTPUT file.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="outputName">The variable name used in the output.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Release notes written to GITHUB_OUTPUT as '{OutputName}'")]
    private static partial void LogGitHubOutputWritten(ILogger logger, string outputName);
}
