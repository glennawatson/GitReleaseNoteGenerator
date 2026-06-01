// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using GitReleaseNoteGenerator.Commands;

namespace GitReleaseNoteGenerator.Tests.Commands;

/// <summary>
/// Tests for <see cref="CommandOptionsFactory"/>.
/// </summary>
public class CommandOptionsFactoryTests
{
    /// <summary>
    /// Tests that the output-name option defaults to the changelog name.
    /// </summary>
    [Test]
    public async Task CreateRootCommand_AppliesOutputNameDefault()
    {
        var options = CommandOptionsFactory.CreateOptions();
        var root = CommandOptionsFactory.CreateRootCommand(options);

        var parseResult = root.Parse([]);

        await Assert.That(parseResult.GetValue(options.OutputNameOption)).IsEqualTo(CommandOptionsFactory.DefaultOutputName);
    }

    /// <summary>
    /// Tests that the root command parses the known options into their values.
    /// </summary>
    [Test]
    public async Task CreateRootCommand_ParsesProvidedOptions()
    {
        var options = CommandOptionsFactory.CreateOptions();
        var root = CommandOptionsFactory.CreateRootCommand(options);

        var parseResult = root.Parse(["--token", "tok", "--owner", "own", "--repo", "rep", "--github-output"]);

        await Assert.That(parseResult.GetValue(options.TokenOption)).IsEqualTo("tok");
        await Assert.That(parseResult.GetValue(options.OwnerOption)).IsEqualTo("own");
        await Assert.That(parseResult.GetValue(options.RepoOption)).IsEqualTo("rep");
        await Assert.That(parseResult.GetValue(options.GitHubOutputOption)).IsTrue();
    }
}
