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
/// End-to-end tests for <see cref="GenerateCommand"/> that invoke the parsed command, capturing
/// console output and the exit code. These mutate global state (console, environment, exit code,
/// the client-factory seam) and must not run in parallel.
/// </summary>
[NotInParallel]
public class GenerateCommandTests
{
    /// <summary>
    /// The --token option name.
    /// </summary>
    private const string TokenArg = "--token";

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
    /// Tests that a missing token reports an error and sets a non-zero exit code.
    /// </summary>
    [Test]
    public async Task Invoke_WithMissingToken_ReportsErrorAndExits()
    {
        var result = await RunAsync([], token: null, repository: null, clientFactory: null);

        await Assert.That(result.ExitCode).IsEqualTo(1);
        await Assert.That(result.StdErr).Contains("GitHub token is required");
    }

    /// <summary>
    /// Tests that a missing repository (token present) reports an error and sets a non-zero exit code.
    /// </summary>
    [Test]
    public async Task Invoke_WithMissingRepository_ReportsErrorAndExits()
    {
        var result = await RunAsync([TokenArg, Token], token: null, repository: null, clientFactory: null);

        await Assert.That(result.ExitCode).IsEqualTo(1);
        await Assert.That(result.StdErr).Contains("Repository owner and name are required");
    }

    /// <summary>
    /// Tests that a missing release version (token and repository present) reports an error.
    /// </summary>
    [Test]
    public async Task Invoke_WithMissingVersion_ReportsErrorAndExits()
    {
        var result = await RunAsync([TokenArg, Token, "--owner", Owner, "--repo", Repo], token: null, repository: null, clientFactory: null);

        await Assert.That(result.ExitCode).IsEqualTo(1);
        await Assert.That(result.StdErr).Contains("Release version is required");
    }

    /// <summary>
    /// Tests the full happy path: a valid invocation renders release notes to stdout.
    /// </summary>
    [Test]
    public async Task Invoke_WithValidArguments_WritesReleaseNotes()
    {
        var handler = new FakeHttpMessageHandler(req =>
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

        var result = await RunAsync(
            [TokenArg, Token, "--owner", Owner, "--repo", Repo, "--release-version", "v9.9.9"],
            token: null,
            repository: null,
            clientFactory: token => GitHubClientFactory.Create(token, handler));

        await Assert.That(result.ExitCode).IsEqualTo(0);
        await Assert.That(result.StdOut).Contains("What's Changed");
        await Assert.That(result.StdOut).Contains("@janedev");
    }

    /// <summary>
    /// Tests that an API failure is caught, reported, and surfaced as a non-zero exit code.
    /// </summary>
    [Test]
    public async Task Invoke_WhenApiFails_ReportsErrorAndExits()
    {
        var handler = new FakeHttpMessageHandler(_ => (HttpStatusCode.Unauthorized, """{ "message": "Bad credentials" }"""));

        var result = await RunAsync(
            [TokenArg, Token, "--owner", Owner, "--repo", Repo, "--release-version", "v9.9.9"],
            token: null,
            repository: null,
            clientFactory: token => GitHubClientFactory.Create(token, handler));

        await Assert.That(result.ExitCode).IsEqualTo(1);
        await Assert.That(result.StdErr).Contains("Error:");
    }

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
    /// Invokes the command with the given arguments while isolating global state (console,
    /// environment variables, exit code, and the client-factory seam), restoring it afterwards.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <param name="token">The value for the GITHUB_TOKEN environment variable (null to unset).</param>
    /// <param name="repository">The value for the GITHUB_REPOSITORY environment variable (null to unset).</param>
    /// <param name="clientFactory">An optional client-factory override.</param>
    /// <returns>The exit code, captured standard output, and captured standard error.</returns>
    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunAsync(
        string[] args,
        string? token,
        string? repository,
        Func<string, GitHubClient>? clientFactory)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        var originalToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        var originalRepository = Environment.GetEnvironmentVariable("GITHUB_REPOSITORY");
        var originalFactory = GenerateCommand.ClientFactory;
        var originalExitCode = Environment.ExitCode;

        var stdout = new StringWriter();
        var stderr = new StringWriter();
        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);
            Environment.SetEnvironmentVariable("GITHUB_TOKEN", token);
            Environment.SetEnvironmentVariable("GITHUB_REPOSITORY", repository);
            Environment.ExitCode = 0;

            if (clientFactory is not null)
            {
                GenerateCommand.ClientFactory = clientFactory;
            }

            await GenerateCommand.Create().Parse(args).InvokeAsync();

            return (Environment.ExitCode, stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
            Environment.SetEnvironmentVariable("GITHUB_TOKEN", originalToken);
            Environment.SetEnvironmentVariable("GITHUB_REPOSITORY", originalRepository);
            GenerateCommand.ClientFactory = originalFactory;
            Environment.ExitCode = originalExitCode;
        }
    }
}
