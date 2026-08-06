// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace GitReleaseNoteGenerator.Tests;

/// <summary>Tests for <see cref="Program"/>. These mutate environment variables and the exit code and must not run in parallel.</summary>
[NotInParallel]
public class ProgramTests
{
    /// <summary>The GITHUB_TOKEN environment variable name.</summary>
    private const string TokenEnv = "GITHUB_TOKEN";

    /// <summary>The GITHUB_REPOSITORY environment variable name.</summary>
    private const string RepositoryEnv = "GITHUB_REPOSITORY";

    /// <summary>Tests that the entry point parses and invokes the command (a missing token fails fast, without any network access).</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task MainWithMissingTokenRunsCommandAndExits()
    {
        var originalToken = Environment.GetEnvironmentVariable(TokenEnv);
        var originalRepository = Environment.GetEnvironmentVariable(RepositoryEnv);
        var originalExitCode = Environment.ExitCode;
        try
        {
            Environment.SetEnvironmentVariable(TokenEnv, null);
            Environment.SetEnvironmentVariable(RepositoryEnv, null);
            Environment.ExitCode = 0;

            await Program.Main([]);

            await Assert.That(Environment.ExitCode).IsEqualTo(1);
        }
        finally
        {
            Environment.SetEnvironmentVariable(TokenEnv, originalToken);
            Environment.SetEnvironmentVariable(RepositoryEnv, originalRepository);
            Environment.ExitCode = originalExitCode;
        }
    }
}
