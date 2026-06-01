// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.CommandLine;
using System.Net;

using GitReleaseNoteGenerator.Commands;
using GitReleaseNoteGenerator.Services;
using GitReleaseNoteGenerator.Tests.Fakes;

using Octokit;

namespace GitReleaseNoteGenerator.Tests.Commands;

/// <summary>
/// End-to-end tests for <see cref="GenerateCommand"/> that invoke the parsed command and assert
/// on the exit code (and, for the happy path, the written output file). These mutate global state
/// (environment variables, exit code, the client-factory seam) and must not run in parallel.
/// </summary>
[NotInParallel]
public class GenerateCommandTests
{
    /// <summary>
    /// The --token option name.
    /// </summary>
    private const string TokenArg = "--token";

    /// <summary>
    /// The --owner option name.
    /// </summary>
    private const string OwnerArg = "--owner";

    /// <summary>
    /// The --repo option name.
    /// </summary>
    private const string RepoArg = "--repo";

    /// <summary>
    /// The token argument value.
    /// </summary>
    private const string Token = "ghp_example";

    /// <summary>
    /// The owner argument value.
    /// </summary>
    private const string Owner = "owner";

    /// <summary>
    /// The repository argument value.
    /// </summary>
    private const string Repo = "repo";

    /// <summary>
    /// A repository payload whose default branch is "main".
    /// </summary>
    private const string RepoJson = """
        { "id": 1, "name": "repo", "full_name": "owner/repo", "default_branch": "main", "owner": { "login": "owner", "id": 1 } }
        """;

    /// <summary>
    /// A single feature commit authored by login "janedev".
    /// </summary>
    private const string CommitJson = """
        {
          "sha": "abc1234",
          "commit": {
            "message": "feat: a shiny new feature",
            "author": { "name": "Jane Dev", "email": "jane@example.com", "date": "2020-01-01T00:00:00Z" },
            "committer": { "name": "Jane Dev", "email": "jane@example.com", "date": "2020-01-01T00:00:00Z" }
          },
          "author": { "login": "janedev", "id": 7 },
          "committer": { "login": "janedev", "id": 7 },
          "parents": []
        }
        """;

    /// <summary>
    /// Tests that a missing token sets a non-zero exit code.
    /// </summary>
    [Test]
    public async Task Invoke_WithMissingToken_Exits()
    {
        var exitCode = await RunAsync([], token: null, repository: null, clientFactory: null);

        await Assert.That(exitCode).IsEqualTo(1);
    }

    /// <summary>
    /// Tests that a missing repository (token present) sets a non-zero exit code.
    /// </summary>
    [Test]
    public async Task Invoke_WithMissingRepository_Exits()
    {
        var exitCode = await RunAsync([TokenArg, Token], token: null, repository: null, clientFactory: null);

        await Assert.That(exitCode).IsEqualTo(1);
    }

    /// <summary>
    /// Tests that a missing release version (token and repository present) sets a non-zero exit code.
    /// </summary>
    [Test]
    public async Task Invoke_WithMissingVersion_Exits()
    {
        var exitCode = await RunAsync([TokenArg, Token, OwnerArg, Owner, RepoArg, Repo], token: null, repository: null, clientFactory: null);

        await Assert.That(exitCode).IsEqualTo(1);
    }

    /// <summary>
    /// Tests the full happy path: a valid invocation writes release notes to the output file.
    /// </summary>
    [Test]
    public async Task Invoke_WithValidArguments_WritesReleaseNotes()
    {
        var handler = HappyPathHandler();
        var outputFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        try
        {
            var exitCode = await RunAsync(
                [TokenArg, Token, OwnerArg, Owner, RepoArg, Repo, "--release-version", "v9.9.9", "--output-file", outputFile],
                token: null,
                repository: null,
                clientFactory: token => GitHubClientFactory.Create(token, handler));

            await Assert.That(exitCode).IsEqualTo(0);

            var notes = await File.ReadAllTextAsync(outputFile);
            await Assert.That(notes).Contains("What's Changed");
            await Assert.That(notes).Contains("@janedev");
        }
        finally
        {
            if (File.Exists(outputFile))
            {
                File.Delete(outputFile);
            }
        }
    }

    /// <summary>
    /// Tests that the happy path also writes to the GITHUB_OUTPUT file when requested.
    /// </summary>
    [Test]
    public async Task Invoke_WithGitHubOutput_WritesToOutputFile()
    {
        var handler = HappyPathHandler();
        var githubOutput = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var originalGithubOutput = Environment.GetEnvironmentVariable("GITHUB_OUTPUT");
        try
        {
            await File.WriteAllTextAsync(githubOutput, string.Empty);
            Environment.SetEnvironmentVariable("GITHUB_OUTPUT", githubOutput);

            var exitCode = await RunAsync(
                [TokenArg, Token, OwnerArg, Owner, RepoArg, Repo, "--release-version", "v9.9.9", "--github-output"],
                token: null,
                repository: null,
                clientFactory: token => GitHubClientFactory.Create(token, handler));

            await Assert.That(exitCode).IsEqualTo(0);

            var written = await File.ReadAllTextAsync(githubOutput);
            await Assert.That(written).Contains("changelog<<");
        }
        finally
        {
            Environment.SetEnvironmentVariable("GITHUB_OUTPUT", originalGithubOutput);
            if (File.Exists(githubOutput))
            {
                File.Delete(githubOutput);
            }
        }
    }

    /// <summary>
    /// Tests that an API failure is caught and surfaced as a non-zero exit code.
    /// </summary>
    [Test]
    public async Task Invoke_WhenApiFails_Exits()
    {
        var handler = new FakeHttpMessageHandler(_ => (HttpStatusCode.Unauthorized, """{ "message": "Bad credentials" }"""));

        var exitCode = await RunAsync(
            [TokenArg, Token, OwnerArg, Owner, RepoArg, Repo, "--release-version", "v9.9.9"],
            token: null,
            repository: null,
            clientFactory: token => GitHubClientFactory.Create(token, handler));

        await Assert.That(exitCode).IsEqualTo(1);
    }

    /// <summary>
    /// Builds a fake handler that serves the no-release (all-history) happy path with one commit.
    /// </summary>
    /// <returns>The configured fake handler.</returns>
    private static FakeHttpMessageHandler HappyPathHandler() =>
        new(req =>
        {
            var path = req.RequestUri!.AbsolutePath;
            if (path == $"/repos/{Owner}/{Repo}")
            {
                return (HttpStatusCode.OK, RepoJson);
            }

            if (path.EndsWith("/releases/latest", StringComparison.Ordinal))
            {
                return (HttpStatusCode.NotFound, """{ "message": "Not Found" }""");
            }

            if (path.EndsWith("/commits", StringComparison.Ordinal))
            {
                return IsBeyondFirstPage(req.RequestUri!.Query)
                    ? (HttpStatusCode.OK, "[]")
                    : (HttpStatusCode.OK, $"[{CommitJson}]");
            }

            return (HttpStatusCode.OK, "[]");
        });

    /// <summary>
    /// Determines whether a request query targets a page beyond the first.
    /// </summary>
    /// <param name="query">The request query string (including the leading '?').</param>
    /// <returns>True if a "page" parameter greater than 1 is requested; otherwise, false.</returns>
    private static bool IsBeyondFirstPage(string query)
    {
        foreach (var part in query.TrimStart('?').Split('&'))
        {
            if (part.StartsWith("page=", StringComparison.Ordinal))
            {
                return part["page=".Length..] != "1";
            }
        }

        return false;
    }

    /// <summary>
    /// Invokes the command with the given arguments while isolating global state (environment
    /// variables, exit code, and the client-factory seam), restoring it afterwards.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <param name="token">The value for the GITHUB_TOKEN environment variable (null to unset).</param>
    /// <param name="repository">The value for the GITHUB_REPOSITORY environment variable (null to unset).</param>
    /// <param name="clientFactory">An optional client-factory override.</param>
    /// <returns>The resulting process exit code.</returns>
    private static async Task<int> RunAsync(
        string[] args,
        string? token,
        string? repository,
        Func<string, GitHubClient>? clientFactory)
    {
        var originalToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        var originalRepository = Environment.GetEnvironmentVariable("GITHUB_REPOSITORY");
        var originalFactory = GenerateCommand.ClientFactory;
        var originalExitCode = Environment.ExitCode;
        try
        {
            Environment.SetEnvironmentVariable("GITHUB_TOKEN", token);
            Environment.SetEnvironmentVariable("GITHUB_REPOSITORY", repository);
            Environment.ExitCode = 0;

            if (clientFactory is not null)
            {
                GenerateCommand.ClientFactory = clientFactory;
            }

            await GenerateCommand.Create().Parse(args).InvokeAsync();

            return Environment.ExitCode;
        }
        finally
        {
            Environment.SetEnvironmentVariable("GITHUB_TOKEN", originalToken);
            Environment.SetEnvironmentVariable("GITHUB_REPOSITORY", originalRepository);
            GenerateCommand.ClientFactory = originalFactory;
            Environment.ExitCode = originalExitCode;
        }
    }
}
