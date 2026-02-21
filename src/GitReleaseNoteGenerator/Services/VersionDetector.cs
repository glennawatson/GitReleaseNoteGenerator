// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

using Microsoft.Extensions.Logging;

namespace GitReleaseNoteGenerator.Services;

/// <summary>
/// Detects the current version using Nerdbank.GitVersioning (nbgv) CLI tool.
/// Cross-platform: runs nbgv directly, falling back to "dotnet nbgv".
/// </summary>
public static partial class VersionDetector
{
    /// <summary>
    /// Attempts to detect the NuGet package version via NBGV.
    /// </summary>
    /// <param name="workingDirectory">The directory to run nbgv in.</param>
    /// <param name="logger">Logger for status messages.</param>
    /// <returns>The detected version string, or null if NBGV is not available.</returns>
    public static async Task<string?> DetectVersionAsync(string workingDirectory, ILogger logger)
    {
        var output = await TryRunAsync("nbgv", "get-version", workingDirectory, logger).ConfigureAwait(false)
                     ?? await TryRunAsync("dotnet", "nbgv get-version", workingDirectory, logger).ConfigureAwait(false);

        if (output is null)
        {
            LogNbgvNotAvailable(logger);
            return null;
        }

        return ParseNuGetPackageVersion(output, logger);
    }

    /// <summary>
    /// Parses the NuGetPackageVersion from nbgv get-version output.
    /// </summary>
    /// <param name="output">The raw output from nbgv.</param>
    /// <param name="logger">Optional logger for warnings.</param>
    /// <returns>The parsed version string, or null if not found.</returns>
    internal static string? ParseNuGetPackageVersion(string output, ILogger? logger = null)
    {
        var lines = output.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            if (line.StartsWith("NuGetPackageVersion", StringComparison.OrdinalIgnoreCase))
            {
                var colonIndex = line.IndexOf(':', StringComparison.Ordinal);
                if (colonIndex >= 0 && colonIndex < line.Length - 1)
                {
                    return line[(colonIndex + 1)..].Trim();
                }
            }
        }

        if (logger is not null)
        {
            LogVersionNotFound(logger);
        }

        return null;
    }

    /// <summary>
    /// Logs that NBGV could not be found on the system.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "NBGV is not available - version auto-detection skipped")]
    private static partial void LogNbgvNotAvailable(ILogger logger);

    /// <summary>
    /// Logs that the NuGetPackageVersion key was not found in the nbgv output.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not find NuGetPackageVersion in nbgv output")]
    private static partial void LogVersionNotFound(ILogger logger);

    /// <summary>
    /// Logs when a process exits with a non-zero exit code.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="fileName">The process file name.</param>
    /// <param name="arguments">The process arguments.</param>
    /// <param name="exitCode">The process exit code.</param>
    /// <param name="error">The standard error output.</param>
    [LoggerMessage(Level = LogLevel.Debug, Message = "'{FileName} {Arguments}' exited with code {ExitCode}: {Error}")]
    private static partial void LogProcessExitedWithError(ILogger logger, string fileName, string arguments, int exitCode, string error);

    /// <summary>
    /// Logs when a process fails to start or throws an exception.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="exception">The exception that occurred.</param>
    /// <param name="fileName">The process file name.</param>
    /// <param name="arguments">The process arguments.</param>
    [LoggerMessage(Level = LogLevel.Debug, Message = "Failed to run '{FileName} {Arguments}'")]
    private static partial void LogProcessFailed(ILogger logger, Exception exception, string fileName, string arguments);

    /// <summary>
    /// Attempts to run a process and capture its standard output.
    /// </summary>
    /// <param name="fileName">The executable to run.</param>
    /// <param name="arguments">The command-line arguments.</param>
    /// <param name="workingDirectory">The working directory for the process.</param>
    /// <param name="logger">Logger for diagnostic messages.</param>
    /// <returns>The standard output if the process exits successfully, or null on failure.</returns>
    private static async Task<string?> TryRunAsync(string fileName, string arguments, string workingDirectory, ILogger logger)
    {
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            process.Start();

            var output = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
            var error = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);

            await process.WaitForExitAsync().ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                LogProcessExitedWithError(logger, fileName, arguments, process.ExitCode, error);
                return null;
            }

            return output;
        }
        catch (Exception ex)
        {
            LogProcessFailed(logger, ex, fileName, arguments);
            return null;
        }
    }
}
