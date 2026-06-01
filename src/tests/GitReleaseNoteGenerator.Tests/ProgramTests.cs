// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace GitReleaseNoteGenerator.Tests;

/// <summary>
/// Tests for <see cref="Program"/>. These mutate environment variables and the exit code and
/// must not run in parallel.
/// </summary>
[NotInParallel]
public class ProgramTests
{
    /// <summary>
    /// Tests that the entry point parses and invokes the command (a missing token fails fast,
    /// without any network access).
    /// </summary>
    [Test]
    public async Task Main_WithMissingToken_RunsCommandAndExits()
    {
        var originalToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        var originalRepository = Environment.GetEnvironmentVariable("GITHUB_REPOSITORY");
        var originalExitCode = Environment.ExitCode;
        try
        {
            Environment.SetEnvironmentVariable("GITHUB_TOKEN", null);
            Environment.SetEnvironmentVariable("GITHUB_REPOSITORY", null);
            Environment.ExitCode = 0;

            await Program.Main([]);

            await Assert.That(Environment.ExitCode).IsEqualTo(1);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GITHUB_TOKEN", originalToken);
            Environment.SetEnvironmentVariable("GITHUB_REPOSITORY", originalRepository);
            Environment.ExitCode = originalExitCode;
        }
    }
}
