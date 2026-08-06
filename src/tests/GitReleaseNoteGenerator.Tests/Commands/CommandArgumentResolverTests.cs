// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using GitReleaseNoteGenerator.Commands;

namespace GitReleaseNoteGenerator.Tests.Commands;

/// <summary>Tests for <see cref="CommandArgumentResolver"/>.</summary>
public class CommandArgumentResolverTests
{
    /// <summary>The GITHUB_TOKEN environment variable name.</summary>
    private const string TokenEnv = "GITHUB_TOKEN";

    /// <summary>The GITHUB_REPOSITORY environment variable name.</summary>
    private const string RepositoryEnv = "GITHUB_REPOSITORY";

    /// <summary>Tests that explicitly-provided options are read in preference to the environment.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ReadValuesWithProvidedOptionsUsesParsedValues()
    {
        var options = CommandOptionsFactory.CreateOptions();
        var root = CommandOptionsFactory.CreateRootCommand(options);
        var parseResult = root.Parse(["--token", "tok", "--owner", "own", "--repo", "rep", "--release-version", "9.9.9"]);

        var values = CommandArgumentResolver.ReadValues(parseResult, options);

        await Assert.That(values.Token).IsEqualTo("tok");
        await Assert.That(values.Owner).IsEqualTo("own");
        await Assert.That(values.Repo).IsEqualTo("rep");
        await Assert.That(values.Version).IsEqualTo("9.9.9");
    }

    /// <summary>Tests that missing options fall back to the GitHub Actions environment.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    [NotInParallel]
    public async Task ReadValuesWhenOptionsAbsentFallsBackToEnvironment()
    {
        var originalToken = Environment.GetEnvironmentVariable(TokenEnv);
        var originalRepo = Environment.GetEnvironmentVariable(RepositoryEnv);
        try
        {
            Environment.SetEnvironmentVariable(TokenEnv, "env-token");
            Environment.SetEnvironmentVariable(RepositoryEnv, "octocat/Hello-World");

            var options = CommandOptionsFactory.CreateOptions();
            var root = CommandOptionsFactory.CreateRootCommand(options);
            var values = CommandArgumentResolver.ReadValues(root.Parse([]), options);

            await Assert.That(values.Token).IsEqualTo("env-token");
            await Assert.That(values.Owner).IsEqualTo("octocat");
            await Assert.That(values.Repo).IsEqualTo("Hello-World");
        }
        finally
        {
            Environment.SetEnvironmentVariable(TokenEnv, originalToken);
            Environment.SetEnvironmentVariable(RepositoryEnv, originalRepo);
        }
    }

    /// <summary>Tests that a missing token is reported.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ValidateWithMissingTokenReturnsTokenMissing()
    {
        var status = CommandArgumentResolver.Validate(CreateValues(token: null));

        await Assert.That(status).IsEqualTo(CommandValidationStatus.TokenMissing);
    }

    /// <summary>Tests that a missing repository is reported when the token is present.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ValidateWithMissingRepoReturnsRepositoryMissing()
    {
        var status = CommandArgumentResolver.Validate(CreateValues(repo: null));

        await Assert.That(status).IsEqualTo(CommandValidationStatus.RepositoryMissing);
    }

    /// <summary>Tests that a missing version is reported when the token and repository are present.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ValidateWithMissingVersionReturnsVersionMissing()
    {
        var status = CommandArgumentResolver.Validate(CreateValues(version: null));

        await Assert.That(status).IsEqualTo(CommandValidationStatus.VersionMissing);
    }

    /// <summary>Tests that complete values validate successfully.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ValidateWithAllRequiredValuesReturnsValid()
    {
        var status = CommandArgumentResolver.Validate(CreateValues());

        await Assert.That(status).IsEqualTo(CommandValidationStatus.Valid);
    }

    /// <summary>Tests that values are mapped into arguments.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task CreateArgumentsMapsValues()
    {
        var values = new GenerateCommandValues("t", "o", "r", "base", "head", "1.2.3", null, true, "out");

        var arguments = CommandArgumentResolver.CreateArguments(values);

        await Assert.That(arguments.Token).IsEqualTo("t");
        await Assert.That(arguments.Owner).IsEqualTo("o");
        await Assert.That(arguments.Repo).IsEqualTo("r");
        await Assert.That(arguments.BaseRef).IsEqualTo("base");
        await Assert.That(arguments.HeadRef).IsEqualTo("head");
        await Assert.That(arguments.Version).IsEqualTo("1.2.3");
        await Assert.That(arguments.GitHubOutput).IsTrue();
        await Assert.That(arguments.OutputName).IsEqualTo("out");
    }

    /// <summary>Creates command values with sensible defaults, overridable per test.</summary>
    /// <param name="token">The token value.</param>
    /// <param name="owner">The owner value.</param>
    /// <param name="repo">The repository value.</param>
    /// <param name="version">The release version value.</param>
    /// <returns>The command values.</returns>
    private static GenerateCommandValues CreateValues(string? token = "t", string? owner = "o", string? repo = "r", string? version = "v1.0.0") =>
        new(token, owner, repo, null, null, version, null, false, CommandOptionsFactory.DefaultOutputName);
}
