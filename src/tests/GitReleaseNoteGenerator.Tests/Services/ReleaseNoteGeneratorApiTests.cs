// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Net;

using GitReleaseNoteGenerator.Services;
using GitReleaseNoteGenerator.Tests.Fakes;

using Microsoft.Extensions.Logging.Abstractions;

namespace GitReleaseNoteGenerator.Tests.Services;

/// <summary>Tests for <see cref="ReleaseNoteGenerator"/>'s GitHub API orchestration, driven end-to-end through a fake HTTP handler (no live network calls).</summary>
public class ReleaseNoteGeneratorApiTests
{
    /// <summary>The repository owner used in the fake API.</summary>
    private const string Owner = "owner";

    /// <summary>The repository name used in the fake API.</summary>
    private const string Repo = "repo";

    /// <summary>The GitHub token supplied to the client factory in the fake-driven tests.</summary>
    private const string Token = "token";

    /// <summary>The commits API path suffix matched by the fake handler.</summary>
    private const string CommitsPath = "/commits";

    /// <summary>The latest-release API path suffix matched by the fake handler.</summary>
    private const string LatestReleasePath = "/releases/latest";

    /// <summary>The compare API path fragment matched by the fake handler.</summary>
    private const string ComparePath = "/compare/";

    /// <summary>The release version requested by the generator tests.</summary>
    private const string Version = "v2.0.0";

    /// <summary>The number of commits the generator requests per page, so a page this size signals another follows.</summary>
    private const int FullPageSize = 100;

    /// <summary>The number of pages served by the multi-page fakes: one full page, then one short page.</summary>
    private const int PagesPerWalk = 2;

    /// <summary>The number of commits across the pages served by the multi-page fakes.</summary>
    private const int TotalPagedCommits = FullPageSize + 1;

    /// <summary>The query parameter prefix carrying the requested page number.</summary>
    private const string PageParameter = "page=";

    /// <summary>The subject line of the single commit served by the fake handler.</summary>
    private const string CommitSubject = "feat: a shiny new feature";

    /// <summary>A repository payload whose default branch is "main".</summary>
    private const string RepoJson = """
        { "id": 1, "name": "repo", "full_name": "owner/repo", "default_branch": "main", "owner": { "login": "owner", "id": 1 } }
        """;

    /// <summary>A single feature commit authored by login "janedev".</summary>
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

    /// <summary>Tests that with no existing release the generator walks all history and renders the commit, category, and contributor.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task GenerateAsyncWithNoExistingReleaseRendersCommitsFromAllHistory()
    {
        var handler = new FakeHttpMessageHandler(static req =>
        {
            var path = req.RequestUri!.AbsolutePath;
            if (path == $"/repos/{Owner}/{Repo}")
            {
                return (HttpStatusCode.OK, RepoJson);
            }

            if (path.EndsWith(LatestReleasePath, StringComparison.Ordinal))
            {
                return (HttpStatusCode.NotFound, """{ "message": "Not Found" }""");
            }

            if (path.EndsWith(CommitsPath, StringComparison.Ordinal))
            {
                return IsBeyondFirstPage(req.RequestUri!.Query)
                    ? (HttpStatusCode.OK, "[]")
                    : (HttpStatusCode.OK, $"[{CommitJson}]");
            }

            return (HttpStatusCode.OK, "[]");
        });

        var generator = new ReleaseNoteGenerator(GitHubClientFactory.Create(Token, handler), NullLogger.Instance);

        var notes = await generator.GenerateAsync(Owner, Repo, Version, null, null);

        await Assert.That(notes).Contains("What's Changed");
        await Assert.That(notes).Contains(CommitSubject);
        await Assert.That(notes).Contains("@janedev");
        await Assert.That(notes).Contains("commits/v2.0.0");
    }

    /// <summary>Tests that an existing release drives the compare-based path and the compare changelog URL.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task GenerateAsyncWithExistingReleaseUsesCompareRange()
    {
        var handler = new FakeHttpMessageHandler(static req =>
        {
            var path = req.RequestUri!.AbsolutePath;
            if (path == $"/repos/{Owner}/{Repo}")
            {
                return (HttpStatusCode.OK, RepoJson);
            }

            if (path.EndsWith(LatestReleasePath, StringComparison.Ordinal))
            {
                return (HttpStatusCode.OK, """{ "id": 1, "tag_name": "v1.0.0", "name": "v1.0.0" }""");
            }

            if (path.Contains(ComparePath, StringComparison.Ordinal))
            {
                return (HttpStatusCode.OK, CompareJson());
            }

            if (path.EndsWith(CommitsPath, StringComparison.Ordinal))
            {
                return IsBeyondFirstPage(req.RequestUri!.Query)
                    ? (HttpStatusCode.OK, "[]")
                    : (HttpStatusCode.OK, $"[{CommitJson}]");
            }

            return (HttpStatusCode.OK, "[]");
        });

        var generator = new ReleaseNoteGenerator(GitHubClientFactory.Create(Token, handler), NullLogger.Instance);

        var notes = await generator.GenerateAsync(Owner, Repo, Version, null, null);

        await Assert.That(notes).Contains(CommitSubject);
        await Assert.That(notes).Contains("compare/v1.0.0...v2.0.0");
    }

    /// <summary>
    /// Tests that a bare release version is aligned to the "v"-prefixed release tag so the compare
    /// link targets the tag that actually exists (regression for changelog links ending in
    /// "...10.0.0" instead of "...v10.0.0").
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task GenerateAsyncWithBareVersionAndVPrefixedReleaseAlignsCompareHeadToTag()
    {
        var handler = new FakeHttpMessageHandler(static req =>
        {
            var path = req.RequestUri!.AbsolutePath;
            if (path == $"/repos/{Owner}/{Repo}")
            {
                return (HttpStatusCode.OK, RepoJson);
            }

            if (path.EndsWith(LatestReleasePath, StringComparison.Ordinal))
            {
                return (HttpStatusCode.OK, """{ "id": 1, "tag_name": "v1.0.0", "name": "v1.0.0" }""");
            }

            if (path.Contains(ComparePath, StringComparison.Ordinal))
            {
                return (HttpStatusCode.OK, CompareJson());
            }

            if (path.EndsWith(CommitsPath, StringComparison.Ordinal))
            {
                return IsBeyondFirstPage(req.RequestUri!.Query)
                    ? (HttpStatusCode.OK, "[]")
                    : (HttpStatusCode.OK, $"[{CommitJson}]");
            }

            return (HttpStatusCode.OK, "[]");
        });

        var generator = new ReleaseNoteGenerator(GitHubClientFactory.Create(Token, handler), NullLogger.Instance);

        var notes = await generator.GenerateAsync(Owner, Repo, "2.0.0", null, null);

        await Assert.That(notes).Contains("compare/v1.0.0...v2.0.0");
        await Assert.That(notes).DoesNotContain("compare/v1.0.0...2.0.0");
    }

    /// <summary>Tests that an explicit base ref skips release lookup and drives the compare path directly.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task GenerateAsyncWithExplicitBaseRefSkipsReleaseLookup()
    {
        var handler = new FakeHttpMessageHandler(static req =>
        {
            var path = req.RequestUri!.AbsolutePath;
            if (path == $"/repos/{Owner}/{Repo}")
            {
                return (HttpStatusCode.OK, RepoJson);
            }

            if (path.Contains(ComparePath, StringComparison.Ordinal))
            {
                return (HttpStatusCode.OK, CompareJson());
            }

            if (path.EndsWith(CommitsPath, StringComparison.Ordinal))
            {
                return IsBeyondFirstPage(req.RequestUri!.Query)
                    ? (HttpStatusCode.OK, "[]")
                    : (HttpStatusCode.OK, $"[{CommitJson}]");
            }

            return (HttpStatusCode.OK, "[]");
        });

        var generator = new ReleaseNoteGenerator(GitHubClientFactory.Create(Token, handler), NullLogger.Instance);

        var notes = await generator.GenerateAsync(Owner, Repo, Version, "v1.5.0", null);

        await Assert.That(notes).Contains(CommitSubject);
        await Assert.That(notes).Contains("compare/v1.5.0...v2.0.0");
    }

    /// <summary>Tests that a comparison larger than one page is fetched page by page until its total commit count is reached.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task GenerateAsyncWithMultiPageComparisonFetchesEveryPage()
    {
        var handler = new FakeHttpMessageHandler(static req =>
        {
            var path = req.RequestUri!.AbsolutePath;
            if (path == $"/repos/{Owner}/{Repo}")
            {
                return (HttpStatusCode.OK, RepoJson);
            }

            return path.Contains(ComparePath, StringComparison.Ordinal)
                ? (HttpStatusCode.OK, $$"""{ "total_commits": {{TotalPagedCommits}}, "commits": [ {{CommitPageJson(GetPage(req.RequestUri!.Query))}} ] }""")
                : (HttpStatusCode.OK, "[]");
        });

        var generator = new ReleaseNoteGenerator(GitHubClientFactory.Create(Token, handler), NullLogger.Instance);

        var notes = await generator.GenerateAsync(Owner, Repo, Version, "v1.5.0", null);

        await Assert.That(notes).Contains("feat: change 0");
        await Assert.That(notes).Contains($"feat: change {FullPageSize}");
        await Assert.That(CountRequests(handler, ComparePath)).IsEqualTo(PagesPerWalk);
    }

    /// <summary>Tests that the all-history walk follows full pages and stops at the first short page without requesting another.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task GenerateAsyncWithMultiPageHistoryStopsAfterShortPage()
    {
        var handler = new FakeHttpMessageHandler(static req =>
        {
            var path = req.RequestUri!.AbsolutePath;
            if (path == $"/repos/{Owner}/{Repo}")
            {
                return (HttpStatusCode.OK, RepoJson);
            }

            if (path.EndsWith(LatestReleasePath, StringComparison.Ordinal))
            {
                return (HttpStatusCode.NotFound, """{ "message": "Not Found" }""");
            }

            return path.EndsWith(CommitsPath, StringComparison.Ordinal)
                ? (HttpStatusCode.OK, $"[{CommitPageJson(GetPage(req.RequestUri!.Query))}]")
                : (HttpStatusCode.OK, "[]");
        });

        var generator = new ReleaseNoteGenerator(GitHubClientFactory.Create(Token, handler), NullLogger.Instance);

        var notes = await generator.GenerateAsync(Owner, Repo, Version, null, null);

        await Assert.That(notes).Contains($"feat: change {FullPageSize}");

        // One walk for the release commits and another for the historical authors.
        await Assert.That(CountRequests(handler, CommitsPath)).IsEqualTo(PagesPerWalk + PagesPerWalk);
    }

    /// <summary>Builds a comparison payload containing the single feature commit.</summary>
    /// <returns>The compare-result JSON.</returns>
    private static string CompareJson() => $$"""
        {
          "status": "ahead",
          "ahead_by": 1,
          "behind_by": 0,
          "total_commits": 1,
          "commits": [ {{CommitJson}} ]
        }
        """;

    /// <summary>Builds the commits served on a page by the multi-page fakes: a full page, then one commit, then nothing.</summary>
    /// <param name="page">The 1-based page number requested.</param>
    /// <returns>The comma-separated commit objects, without the enclosing array brackets.</returns>
    private static string CommitPageJson(int page) => page switch
    {
        1 => FullPageJson(),
        PagesPerWalk => NumberedCommitJson(FullPageSize),
        _ => string.Empty,
    };

    /// <summary>Builds the comma-separated JSON of a full page of distinct feature commits.</summary>
    /// <returns>The commit objects, without the enclosing array brackets.</returns>
    private static string FullPageJson()
    {
        var commits = new string[FullPageSize];
        for (var i = 0; i < commits.Length; i++)
        {
            commits[i] = NumberedCommitJson(i);
        }

        return string.Join(',', commits);
    }

    /// <summary>Builds the JSON of a feature commit with a subject and SHA derived from a number.</summary>
    /// <param name="number">The number that makes the commit distinct.</param>
    /// <returns>The commit JSON.</returns>
    private static string NumberedCommitJson(int number) => $$"""
        {
          "sha": "sha{{number}}",
          "commit": {
            "message": "feat: change {{number}}",
            "author": { "name": "Octo Cat", "email": "octocat@example.com", "date": "2020-01-01T00:00:00Z" }
          },
          "author": { "login": "octocat", "id": 1 },
          "parents": []
        }
        """;

    /// <summary>Reads the requested page number from a request query, treating an absent page as the first.</summary>
    /// <param name="query">The request query string (including the leading '?').</param>
    /// <returns>The 1-based page number.</returns>
    private static int GetPage(string query)
    {
        foreach (var part in query.TrimStart('?').Split('&'))
        {
            if (part.StartsWith(PageParameter, StringComparison.Ordinal))
            {
                return int.Parse(part[PageParameter.Length..], CultureInfo.InvariantCulture);
            }
        }

        return 1;
    }

    /// <summary>Counts the handled requests whose path contains a fragment.</summary>
    /// <param name="handler">The fake handler that recorded the requests.</param>
    /// <param name="pathFragment">The path fragment to match.</param>
    /// <returns>The number of matching requests.</returns>
    private static int CountRequests(FakeHttpMessageHandler handler, string pathFragment)
    {
        var count = 0;
        foreach (var request in handler.Requests)
        {
            if (request.RequestUri!.AbsolutePath.Contains(pathFragment, StringComparison.Ordinal))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Determines whether a request query targets a page beyond the first (used to terminate the
    /// historical-author pagination loop).
    /// </summary>
    /// <param name="query">The request query string (including the leading '?').</param>
    /// <returns>True if a "page" parameter greater than 1 is requested; otherwise, false.</returns>
    private static bool IsBeyondFirstPage(string query) => GetPage(query) != 1;
}
