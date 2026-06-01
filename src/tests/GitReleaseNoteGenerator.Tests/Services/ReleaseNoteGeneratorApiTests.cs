// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net;

using GitReleaseNoteGenerator.Services;
using GitReleaseNoteGenerator.Tests.Fakes;

using Microsoft.Extensions.Logging.Abstractions;

namespace GitReleaseNoteGenerator.Tests.Services;

/// <summary>
/// Tests for <see cref="ReleaseNoteGenerator"/>'s GitHub API orchestration, driven end-to-end
/// through a fake HTTP handler (no live network calls).
/// </summary>
public class ReleaseNoteGeneratorApiTests
{
    /// <summary>
    /// The repository owner used in the fake API.
    /// </summary>
    private const string Owner = "owner";

    /// <summary>
    /// The repository name used in the fake API.
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
    /// Tests that with no existing release the generator walks all history and renders the
    /// commit, category, and contributor.
    /// </summary>
    [Test]
    public async Task GenerateAsync_WithNoExistingRelease_RendersCommitsFromAllHistory()
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

        var generator = new ReleaseNoteGenerator(GitHubClientFactory.Create("token", handler), NullLogger.Instance);

        var notes = await generator.GenerateAsync(Owner, Repo, "v2.0.0", null, null);

        await Assert.That(notes).Contains("What's Changed");
        await Assert.That(notes).Contains("feat: a shiny new feature");
        await Assert.That(notes).Contains("@janedev");
        await Assert.That(notes).Contains("commits/v2.0.0");
    }

    /// <summary>
    /// Tests that an existing release drives the compare-based path and the compare changelog URL.
    /// </summary>
    [Test]
    public async Task GenerateAsync_WithExistingRelease_UsesCompareRange()
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
                return (HttpStatusCode.OK, """{ "id": 1, "tag_name": "v1.0.0", "name": "v1.0.0" }""");
            }

            if (path.Contains("/compare/", StringComparison.Ordinal))
            {
                return (HttpStatusCode.OK, CompareJson());
            }

            if (path.EndsWith("/commits", StringComparison.Ordinal))
            {
                return IsBeyondFirstPage(req.RequestUri!.Query)
                    ? (HttpStatusCode.OK, "[]")
                    : (HttpStatusCode.OK, $"[{CommitJson}]");
            }

            return (HttpStatusCode.OK, "[]");
        });

        var generator = new ReleaseNoteGenerator(GitHubClientFactory.Create("token", handler), NullLogger.Instance);

        var notes = await generator.GenerateAsync(Owner, Repo, "v2.0.0", null, null);

        await Assert.That(notes).Contains("feat: a shiny new feature");
        await Assert.That(notes).Contains("compare/v1.0.0...v2.0.0");
    }

    /// <summary>
    /// Tests that an explicit base ref skips release lookup and drives the compare path directly.
    /// </summary>
    [Test]
    public async Task GenerateAsync_WithExplicitBaseRef_SkipsReleaseLookup()
    {
        var handler = new FakeHttpMessageHandler(req =>
        {
            var path = req.RequestUri!.AbsolutePath;
            if (path == $"/repos/{Owner}/{Repo}")
            {
                return (HttpStatusCode.OK, RepoJson);
            }

            if (path.Contains("/compare/", StringComparison.Ordinal))
            {
                return (HttpStatusCode.OK, CompareJson());
            }

            if (path.EndsWith("/commits", StringComparison.Ordinal))
            {
                return IsBeyondFirstPage(req.RequestUri!.Query)
                    ? (HttpStatusCode.OK, "[]")
                    : (HttpStatusCode.OK, $"[{CommitJson}]");
            }

            return (HttpStatusCode.OK, "[]");
        });

        var generator = new ReleaseNoteGenerator(GitHubClientFactory.Create("token", handler), NullLogger.Instance);

        var notes = await generator.GenerateAsync(Owner, Repo, "v2.0.0", "v1.5.0", null);

        await Assert.That(notes).Contains("feat: a shiny new feature");
        await Assert.That(notes).Contains("compare/v1.5.0...v2.0.0");
    }

    /// <summary>
    /// Builds a comparison payload containing the single feature commit.
    /// </summary>
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

    /// <summary>
    /// Determines whether a request query targets a page beyond the first (used to terminate the
    /// historical-author pagination loop).
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
}
