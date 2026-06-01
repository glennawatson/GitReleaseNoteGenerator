// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using GitReleaseNoteGenerator.Commands;

namespace GitReleaseNoteGenerator.Tests.Commands;

/// <summary>
/// Tests for <see cref="CommandArgumentResolver"/>.
/// </summary>
public class CommandArgumentResolverTests
{
    /// <summary>
    /// Tests that explicitly-provided options are read in preference to the environment.
    /// </summary>
    [Test]
    public async Task ReadValues_WithProvidedOptions_UsesParsedValues()
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

    /// <summary>
    /// Tests that missing options fall back to the GitHub Actions environment.
    /// </summary>
    [Test]
    [NotInParallel]
    public async Task ReadValues_WhenOptionsAbsent_FallsBackToEnvironment()
    {
        var originalToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        var originalRepo = Environment.GetEnvironmentVariable("GITHUB_REPOSITORY");
        try
        {
            Environment.SetEnvironmentVariable("GITHUB_TOKEN", "env-token");
            Environment.SetEnvironmentVariable("GITHUB_REPOSITORY", "octocat/Hello-World");

            var options = CommandOptionsFactory.CreateOptions();
            var root = CommandOptionsFactory.CreateRootCommand(options);
            var values = CommandArgumentResolver.ReadValues(root.Parse([]), options);

            await Assert.That(values.Token).IsEqualTo("env-token");
            await Assert.That(values.Owner).IsEqualTo("octocat");
            await Assert.That(values.Repo).IsEqualTo("Hello-World");
        }
        finally
        {
            Environment.SetEnvironmentVariable("GITHUB_TOKEN", originalToken);
            Environment.SetEnvironmentVariable("GITHUB_REPOSITORY", originalRepo);
        }
    }

    /// <summary>
    /// Tests that a missing token is reported.
    /// </summary>
    [Test]
    public async Task Validate_WithMissingToken_ReturnsTokenMissing()
    {
        var status = CommandArgumentResolver.Validate(CreateValues(token: null));

        await Assert.That(status).IsEqualTo(CommandValidationStatus.TokenMissing);
    }

    /// <summary>
    /// Tests that a missing repository is reported when the token is present.
    /// </summary>
    [Test]
    public async Task Validate_WithMissingRepo_ReturnsRepositoryMissing()
    {
        var status = CommandArgumentResolver.Validate(CreateValues(repo: null));

        await Assert.That(status).IsEqualTo(CommandValidationStatus.RepositoryMissing);
    }

    /// <summary>
    /// Tests that complete values validate successfully.
    /// </summary>
    [Test]
    public async Task Validate_WithAllRequiredValues_ReturnsValid()
    {
        var status = CommandArgumentResolver.Validate(CreateValues());

        await Assert.That(status).IsEqualTo(CommandValidationStatus.Valid);
    }

    /// <summary>
    /// Tests that values and the resolved version are mapped into arguments.
    /// </summary>
    [Test]
    public async Task CreateArguments_MapsValuesAndVersion()
    {
        var values = new GenerateCommandValues("t", "o", "r", "base", "head", null, null, true, "out");

        var arguments = CommandArgumentResolver.CreateArguments(values, "1.2.3");

        await Assert.That(arguments.Token).IsEqualTo("t");
        await Assert.That(arguments.Owner).IsEqualTo("o");
        await Assert.That(arguments.Repo).IsEqualTo("r");
        await Assert.That(arguments.BaseRef).IsEqualTo("base");
        await Assert.That(arguments.HeadRef).IsEqualTo("head");
        await Assert.That(arguments.Version).IsEqualTo("1.2.3");
        await Assert.That(arguments.GitHubOutput).IsTrue();
        await Assert.That(arguments.OutputName).IsEqualTo("out");
    }

    /// <summary>
    /// Creates command values with sensible defaults, overridable per test.
    /// </summary>
    /// <param name="token">The token value.</param>
    /// <param name="owner">The owner value.</param>
    /// <param name="repo">The repository value.</param>
    /// <returns>The command values.</returns>
    private static GenerateCommandValues CreateValues(string? token = "t", string? owner = "o", string? repo = "r") =>
        new(token, owner, repo, null, null, null, null, false, CommandOptionsFactory.DefaultOutputName);
}
