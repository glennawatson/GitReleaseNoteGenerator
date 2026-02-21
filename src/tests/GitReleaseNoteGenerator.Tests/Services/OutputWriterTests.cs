// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using GitReleaseNoteGenerator.Infrastructure;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace GitReleaseNoteGenerator.Tests.Services;

/// <summary>
/// Tests for <see cref="OutputWriter"/>.
/// These tests mutate the GITHUB_OUTPUT environment variable and must not run in parallel.
/// </summary>
[NotInParallel]
public class OutputWriterTests
{
    /// <summary>
    /// Tests that WriteToFileAsync creates a file with the expected content.
    /// </summary>
    [Test]
    public async Task WriteToFileAsync_CreatesFileWithContent()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var fileInfo = new FileInfo(tempFile);
            var content = "## Release Notes\n\nSome content here.";

            await OutputWriter.WriteToFileAsync(content, fileInfo, NullLogger.Instance);

            var result = await File.ReadAllTextAsync(tempFile);
            await Assert.That(result).IsEqualTo(content);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Tests that WriteToGitHubOutputAsync writes heredoc format to the output file.
    /// </summary>
    [Test]
    public async Task WriteToGitHubOutputAsync_WritesHeredocFormat()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempFile, string.Empty);
            Environment.SetEnvironmentVariable("GITHUB_OUTPUT", tempFile);

            var content = "## What's Changed\n\nSome changes.";
            await OutputWriter.WriteToGitHubOutputAsync(content, "changelog", NullLogger.Instance);

            var result = await File.ReadAllTextAsync(tempFile);

            await Assert.That(result).Contains("changelog<<ghadelimiter_");
            await Assert.That(result).Contains("## What's Changed");
            await Assert.That(result).Contains("Some changes.");
        }
        finally
        {
            Environment.SetEnvironmentVariable("GITHUB_OUTPUT", null);
            File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Tests that WriteToGitHubOutputAsync does nothing when GITHUB_OUTPUT is not set.
    /// </summary>
    [Test]
    public async Task WriteToGitHubOutputAsync_WithNoEnvVar_DoesNothing()
    {
        Environment.SetEnvironmentVariable("GITHUB_OUTPUT", null);

        // Should not throw
        await OutputWriter.WriteToGitHubOutputAsync("content", "changelog", NullLogger.Instance);
    }
}
