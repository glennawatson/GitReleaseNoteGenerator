// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using GitReleaseNoteGenerator.Infrastructure;

namespace GitReleaseNoteGenerator.Tests.Infrastructure;

/// <summary>
/// Tests for <see cref="GitHubActionEnvironment"/>. These mutate process environment
/// variables and must not run in parallel.
/// </summary>
[NotInParallel]
public class GitHubActionEnvironmentTests
{
    /// <summary>
    /// The GITHUB_REPOSITORY environment variable name.
    /// </summary>
    private const string RepositoryEnv = "GITHUB_REPOSITORY";

    /// <summary>
    /// A sample "owner/repo" slug.
    /// </summary>
    private const string RepoSlug = "octocat/Hello-World";

    /// <summary>
    /// Tests that the token is read from GITHUB_TOKEN.
    /// </summary>
    [Test]
    public async Task Token_WhenSet_ReturnsValue()
    {
        var token = WithEnv("GITHUB_TOKEN", "ghp_example", () => GitHubActionEnvironment.Token);

        await Assert.That(token).IsEqualTo("ghp_example");
    }

    /// <summary>
    /// Tests that the owner is parsed from the "owner/repo" slug.
    /// </summary>
    [Test]
    public async Task RepositoryOwner_WithOwnerSlashRepo_ReturnsOwner()
    {
        var owner = WithEnv(RepositoryEnv, RepoSlug, () => GitHubActionEnvironment.RepositoryOwner);

        await Assert.That(owner).IsEqualTo("octocat");
    }

    /// <summary>
    /// Tests that the repository name is parsed from the "owner/repo" slug.
    /// </summary>
    [Test]
    public async Task RepositoryName_WithOwnerSlashRepo_ReturnsName()
    {
        var name = WithEnv(RepositoryEnv, RepoSlug, () => GitHubActionEnvironment.RepositoryName);

        await Assert.That(name).IsEqualTo("Hello-World");
    }

    /// <summary>
    /// Tests that an unset GITHUB_REPOSITORY yields a null owner.
    /// </summary>
    [Test]
    public async Task RepositoryOwner_WhenUnset_ReturnsNull()
    {
        var owner = WithEnv(RepositoryEnv, null, () => GitHubActionEnvironment.RepositoryOwner);

        await Assert.That(owner).IsNull();
    }

    /// <summary>
    /// Tests that a slug without a slash yields a null repository name.
    /// </summary>
    [Test]
    public async Task RepositoryName_WithoutSlash_ReturnsNull()
    {
        var name = WithEnv(RepositoryEnv, "noslash", () => GitHubActionEnvironment.RepositoryName);

        await Assert.That(name).IsNull();
    }

    /// <summary>
    /// Tests that the output file path is read from GITHUB_OUTPUT.
    /// </summary>
    [Test]
    public async Task OutputFile_WhenSet_ReturnsValue()
    {
        var outputFile = WithEnv("GITHUB_OUTPUT", "/tmp/github_output", () => GitHubActionEnvironment.OutputFile);

        await Assert.That(outputFile).IsEqualTo("/tmp/github_output");
    }

    /// <summary>
    /// Sets an environment variable for the duration of <paramref name="read"/>, then restores it.
    /// </summary>
    /// <param name="name">The environment variable name.</param>
    /// <param name="value">The temporary value (null to unset).</param>
    /// <param name="read">The accessor to evaluate while the variable is set.</param>
    /// <returns>The value returned by <paramref name="read"/>.</returns>
    private static string? WithEnv(string name, string? value, Func<string?> read)
    {
        var original = Environment.GetEnvironmentVariable(name);
        try
        {
            Environment.SetEnvironmentVariable(name, value);
            return read();
        }
        finally
        {
            Environment.SetEnvironmentVariable(name, original);
        }
    }
}
