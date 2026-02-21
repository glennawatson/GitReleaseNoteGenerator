// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using GitReleaseNoteGenerator.Commands;

namespace GitReleaseNoteGenerator;

/// <summary>
/// Entry point for the git-release-notes CLI tool.
/// </summary>
internal static class Program
{
    /// <summary>
    /// Main entry point.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>The exit code.</returns>
    public static Task<int> Main(string[] args)
    {
        var rootCommand = GenerateCommand.Create();
        return rootCommand.Parse(args).InvokeAsync();
    }
}
