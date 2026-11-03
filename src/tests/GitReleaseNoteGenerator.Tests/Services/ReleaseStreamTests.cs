// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net;

using GitReleaseNoteGenerator.Services;
using GitReleaseNoteGenerator.Tests.Fakes;

using Microsoft.Extensions.Logging.Abstractions;

namespace GitReleaseNoteGenerator.Tests.Services;

/// <summary>Verifies release stream selection and changelog links through the HTTP transport.</summary>
public class ReleaseStreamTests
{
    /// <summary>The fake authentication value.</summary>
    private const string Token = "token";

    /// <summary>The test repository owner.</summary>
    private const string Owner = "owner";

    /// <summary>The built commit used for comparisons.</summary>
    private const string BuiltSha = "built-sha";

    /// <summary>The binding stream prefix.</summary>
    private const string BindingPrefix = "binding-v";

    /// <summary>The releases API path suffix.</summary>
    private const string ReleasesPath = "/releases";

    /// <summary>The latest release API path suffix.</summary>
    private const string LatestPath = "/releases/latest";

    /// <summary>The compare API path fragment.</summary>
    private const string ComparePath = "/compare/";

    /// <summary>The commits API path suffix.</summary>
    private const string CommitsPath = "/commits";

    /// <summary>The binding release version.</summary>
    private const string BindingVersion = "9.2.0";

    /// <summary>The maximum releases requested per page.</summary>
    private const int PageSize = 100;

    /// <summary>Releases from two streams, including unpublished and pre-release candidates.</summary>
    private const string ReleasesJson = """
        [
          null,
          { "tag_name": "v4.2.0", "published_at": "2026-11-01T00:00:00Z" },
          { "tag_name": "binding-v9.1.0", "published_at": "2026-10-01T00:00:00Z" },
          { "tag_name": "binding-v9.0.0", "published_at": "2026-09-01T00:00:00Z" },
          { "tag_name": "binding-v9.2.0", "published_at": "2026-11-02T00:00:00Z" },
          { "tag_name": "binding-v10.0.0", "draft": true, "published_at": "2026-11-03T00:00:00Z" },
          { "tag_name": "binding-v10.0.0-rc.1", "prerelease": true, "published_at": "2026-11-03T00:00:00Z" },
          { "tag_name": "binding-v11.0.0" },
          { "tag_name": null, "published_at": "2026-11-03T00:00:00Z" }
        ]
        """;

    /// <summary>Verifies independent baselines, exclusion of the new tag, and custom changelog tags.</summary>
    /// <param name="prefix">The selected stream.</param>
    /// <param name="version">The new version, bare or prefixed.</param>
    /// <param name="releaseTag">An optional exact new tag.</param>
    /// <param name="expectedBase">The previous stable tag in that stream.</param>
    /// <param name="expectedTag">The expected new tag in the link.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    [Arguments("binding-v", "9.2.0", null, "binding-v9.1.0", "binding-v9.2.0")]
    [Arguments("binding-v", "binding-v9.2.0", null, "binding-v9.1.0", "binding-v9.2.0")]
    [Arguments("binding-v", "9.3.0", "binding-v9.2.0", "binding-v9.1.0", "binding-v9.2.0")]
    [Arguments("v", "4.3.0", null, "v4.2.0", "v4.3.0")]
    public async Task GenerateAsyncSelectsOnlyPublishedStableReleasesInStream(
        string prefix,
        string version,
        string? releaseTag,
        string expectedBase,
        string expectedTag)
    {
        using var handler = CreateHandler(static _ => ReleasesJson);
        var generator = new ReleaseNoteGenerator(GitHubClientFactory.Create(Token, handler), NullLogger.Instance);

        var notes = await generator.GenerateAsync(Owner, "repo", version, null, BuiltSha, new(prefix, releaseTag), CancellationToken.None);

        await Assert.That(notes).Contains($"compare/{expectedBase}...{expectedTag}");
        await Assert.That(handler.Requests.Exists(request => Uri.UnescapeDataString(request.RequestUri!.AbsolutePath)
            .EndsWith($"/compare/{expectedBase}...built-sha", StringComparison.Ordinal))).IsTrue();
        await Assert.That(handler.Requests.Exists(static request => request.RequestUri!.AbsolutePath.EndsWith(LatestPath, StringComparison.Ordinal))).IsFalse();
    }

    /// <summary>Verifies that a new stream uses all history without borrowing another stream's baseline.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task GenerateAsyncWithNoMatchingReleaseUsesHistoryAndPrefixedTag()
    {
        using var handler = CreateHandler(static _ => ReleasesJson);
        var generator = new ReleaseNoteGenerator(GitHubClientFactory.Create(Token, handler), NullLogger.Instance);

        var notes = await generator.GenerateAsync(Owner, "repo", "1.0.0", null, BuiltSha, new("other-v"), CancellationToken.None);

        await Assert.That(notes).Contains("commits/other-v1.0.0");
        await Assert.That(handler.Requests.Exists(static request => request.RequestUri!.AbsolutePath.Contains(ComparePath, StringComparison.Ordinal))).IsFalse();
        await Assert.That(handler.Requests.Exists(static request => request.RequestUri!.Query.Contains("sha=built-sha", StringComparison.Ordinal))).IsTrue();
    }

    /// <summary>Verifies that an explicit baseline overrides prefix discovery for a backport.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task GenerateAsyncWithExplicitBaseSkipsReleaseDiscovery()
    {
        using var handler = CreateHandler(static _ => ReleasesJson);
        var generator = new ReleaseNoteGenerator(GitHubClientFactory.Create(Token, handler), NullLogger.Instance);

        var notes = await generator.GenerateAsync(Owner, "repo", "9.0.1", "binding-v9.0.0", BuiltSha, new(BindingPrefix), CancellationToken.None);

        await Assert.That(notes).Contains("compare/binding-v9.0.0...binding-v9.0.1");
        await Assert.That(handler.Requests.Exists(static request => request.RequestUri!.AbsolutePath.Contains(ReleasesPath, StringComparison.Ordinal))).IsFalse();
    }

    /// <summary>Verifies that an exact tag works without opting into prefix-based discovery.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task GenerateAsyncWithExactTagUsesLegacyBaseline()
    {
        using var handler = CreateHandler(static _ => ReleasesJson);
        var generator = new ReleaseNoteGenerator(GitHubClientFactory.Create(Token, handler), NullLogger.Instance);

        var notes = await generator.GenerateAsync(Owner, "repo", BindingVersion, null, null, new(ReleaseTag: "binding-v9.2.0"), CancellationToken.None);

        await Assert.That(notes).Contains("compare/v4.2.0...binding-v9.2.0");
        await Assert.That(handler.Requests.Exists(static request => request.RequestUri!.AbsolutePath.EndsWith(LatestPath, StringComparison.Ordinal))).IsTrue();
    }

    /// <summary>Verifies that discovery scans beyond a full page and uses publication time.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task GenerateAsyncFindsMostRecentlyPublishedMatchAcrossPages()
    {
        var firstPageItems = string.Join(',', Enumerable.Repeat("""{ "tag_name": "v4.2.0", "published_at": "2026-11-01T00:00:00Z" }""", PageSize));
        var firstPage = $"[{firstPageItems}]";
        using var handler = CreateHandler(request => request.RequestUri!.Query.Contains("&page=1", StringComparison.Ordinal)
            ? firstPage
            : ReleasesJson);
        var generator = new ReleaseNoteGenerator(GitHubClientFactory.Create(Token, handler), NullLogger.Instance);

        var notes = await generator.GenerateAsync(Owner, "repo", "9.3.0", null, null, new(BindingPrefix), CancellationToken.None);

        await Assert.That(notes).Contains("compare/binding-v9.2.0...binding-v9.3.0");
        await Assert.That(handler.Requests.Exists(static request => request.RequestUri!.AbsolutePath.EndsWith(ReleasesPath, StringComparison.Ordinal)
            && request.RequestUri.Query.Contains("page=2", StringComparison.Ordinal))).IsTrue();
    }

    /// <summary>Verifies that a cancelled discovery request is observed.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task GenerateAsyncObservesCancellation()
    {
        using var handler = CreateHandler(static _ => ReleasesJson);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var generator = new ReleaseNoteGenerator(GitHubClientFactory.Create(Token, handler), NullLogger.Instance);

        await Assert.That(async () =>
        {
            _ = await generator.GenerateAsync(Owner, "repo", BindingVersion, null, null, new(BindingPrefix), cancellation.Token);
        })
            .Throws<OperationCanceledException>();
    }

    /// <summary>Verifies fallback refs and rendering when optional GitHub fields are absent.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task GenerateAsyncWithMissingOptionalFieldsUsesFallbacks()
    {
        using var handler = new FakeHttpMessageHandler(static request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith(LatestPath, StringComparison.Ordinal))
            {
                return (HttpStatusCode.OK, "null");
            }

            return path.EndsWith(CommitsPath, StringComparison.Ordinal)
                ? (HttpStatusCode.OK, """[{ "commit": {}, "author": { "login": "octocat" } }]""")
                : (HttpStatusCode.OK, "{}");
        });
        var generator = new ReleaseNoteGenerator(GitHubClientFactory.Create(Token, handler), NullLogger.Instance);

        var notes = await generator.GenerateAsync(Owner, "repo", "1.0.0", null, null);

        await Assert.That(notes).Contains("owner/repo@unknown");
        await Assert.That(handler.Requests.Exists(static request => request.RequestUri!.Query.Contains("sha=HEAD", StringComparison.Ordinal))).IsTrue();
    }

    /// <summary>Verifies that an extensive author history respects the existing pagination cap.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task GenerateAsyncWithExtensiveHistoryStopsAtPaginationCap()
    {
        const int maximumHistoryPages = 500;
        var items = string.Join(',', Enumerable.Repeat("""{ "sha": "abc1234", "commit": {}, "author": { "login": "octocat" } }""", PageSize));
        var pageJson = $"[{items}]";
        var historyRequests = 0;
        using var handler = new FakeHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith(CommitsPath, StringComparison.Ordinal))
            {
                historyRequests++;
                return (HttpStatusCode.OK, pageJson);
            }

            return request.RequestUri.AbsolutePath.Contains(ComparePath, StringComparison.Ordinal)
                ? (HttpStatusCode.OK, """{ "commits": [], "total_commits": 0 }""")
                : (HttpStatusCode.OK, """{ "default_branch": "main" }""");
        });
        var generator = new ReleaseNoteGenerator(GitHubClientFactory.Create(Token, handler), NullLogger.Instance);

        var notes = await generator.GenerateAsync(Owner, "repo", BindingVersion, "binding-v9.1.0", BuiltSha, new(BindingPrefix), CancellationToken.None);

        await Assert.That(historyRequests).IsEqualTo(maximumHistoryPages);
        await Assert.That(notes).Contains("compare/binding-v9.1.0...binding-v9.2.0");
    }

    /// <summary>Creates a fake repository with no commits and configurable release pages.</summary>
    /// <param name="releasePage">Returns the releases payload for a request.</param>
    /// <returns>The fake HTTP handler.</returns>
    private static FakeHttpMessageHandler CreateHandler(Func<HttpRequestMessage, string> releasePage) =>
        new(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith(ReleasesPath, StringComparison.Ordinal))
            {
                return (HttpStatusCode.OK, releasePage(request));
            }

            if (path.EndsWith(LatestPath, StringComparison.Ordinal))
            {
                return (HttpStatusCode.OK, """{ "tag_name": "v4.2.0" }""");
            }

            if (path.Contains(ComparePath, StringComparison.Ordinal))
            {
                return (HttpStatusCode.OK, """{ "commits": [], "total_commits": 0 }""");
            }

            return path.EndsWith(CommitsPath, StringComparison.Ordinal)
                ? (HttpStatusCode.OK, "[]")
                : (HttpStatusCode.OK, """{ "default_branch": "main" }""");
        });
}
