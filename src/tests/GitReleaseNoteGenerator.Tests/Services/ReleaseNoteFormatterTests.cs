// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using GitReleaseNoteGenerator.Services;

using Octokit;

namespace GitReleaseNoteGenerator.Tests.Services;

/// <summary>
/// Tests for <see cref="ReleaseNoteGenerator.FormatReleaseNotes"/>.
/// </summary>
public class ReleaseNoteFormatterTests
{
    /// <summary>
    /// Tests that the output contains the What's Changed header.
    /// </summary>
    [Test]
    public async Task FormatReleaseNotes_ContainsWhatsChangedHeader()
    {
        var grouped = new Dictionary<string, List<GitHubCommit>>(StringComparer.OrdinalIgnoreCase);
        var allAuthors = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var newAuthors = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        var result = ReleaseNoteGenerator.FormatReleaseNotes(
            "owner",
            "repo",
            "https://github.com/owner/repo/compare/v1.0...v2.0",
            allAuthors,
            newAuthors,
            grouped);

        await Assert.That(result).Contains("What's Changed");
    }

    /// <summary>
    /// Tests that the output contains the full changelog link.
    /// </summary>
    [Test]
    public async Task FormatReleaseNotes_ContainsFullChangelogLink()
    {
        var grouped = new Dictionary<string, List<GitHubCommit>>(StringComparer.OrdinalIgnoreCase);
        var allAuthors = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var newAuthors = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        var result = ReleaseNoteGenerator.FormatReleaseNotes(
            "owner",
            "repo",
            "https://github.com/owner/repo/compare/v1.0...v2.0",
            allAuthors,
            newAuthors,
            grouped);

        await Assert.That(result).Contains("**Full Changelog**: https://github.com/owner/repo/compare/v1.0...v2.0");
    }

    /// <summary>
    /// Tests that grouped commits appear under the correct category heading.
    /// </summary>
    [Test]
    public async Task FormatReleaseNotes_WithCommits_ShowsCategoryHeadings()
    {
        var commit = CreateCommit("feat: new thing", "abc123");
        var grouped = new Dictionary<string, List<GitHubCommit>>(StringComparer.OrdinalIgnoreCase)
        {
            { "Features", [commit] },
        };

        var allAuthors = new SortedSet<string>(StringComparer.OrdinalIgnoreCase) { "testuser" };
        var newAuthors = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        var result = ReleaseNoteGenerator.FormatReleaseNotes(
            "owner",
            "repo",
            "https://github.com/owner/repo/compare/v1.0...v2.0",
            allAuthors,
            newAuthors,
            grouped);

        await Assert.That(result).Contains("Features");
        await Assert.That(result).Contains("owner/repo@abc123");
    }

    /// <summary>
    /// Tests that new contributors are listed separately.
    /// </summary>
    [Test]
    public async Task FormatReleaseNotes_WithNewAuthors_ShowsNewContributors()
    {
        var grouped = new Dictionary<string, List<GitHubCommit>>(StringComparer.OrdinalIgnoreCase);
        var allAuthors = new SortedSet<string>(StringComparer.OrdinalIgnoreCase) { "newuser" };
        var newAuthors = new SortedSet<string>(StringComparer.OrdinalIgnoreCase) { "newuser" };

        var result = ReleaseNoteGenerator.FormatReleaseNotes(
            "owner",
            "repo",
            "https://github.com/owner/repo/compare/v1.0...v2.0",
            allAuthors,
            newAuthors,
            grouped);

        await Assert.That(result).Contains("New contributors since the last release");
        await Assert.That(result).Contains("@newuser");
    }

    /// <summary>
    /// Tests that bot contributors are listed separately.
    /// </summary>
    [Test]
    public async Task FormatReleaseNotes_WithBotAuthors_ShowsBotSection()
    {
        var grouped = new Dictionary<string, List<GitHubCommit>>(StringComparer.OrdinalIgnoreCase);
        var allAuthors = new SortedSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "octocat",
            "dependabot[bot]",
        };
        var newAuthors = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        var result = ReleaseNoteGenerator.FormatReleaseNotes(
            "owner",
            "repo",
            "https://github.com/owner/repo/compare/v1.0...v2.0",
            allAuthors,
            newAuthors,
            grouped);

        await Assert.That(result).Contains("Automated services that contributed");
        await Assert.That(result).Contains("@dependabot[bot]");
        await Assert.That(result).Contains("Thanks to all the contributors");
        await Assert.That(result).Contains("@octocat");
    }

    /// <summary>
    /// Tests that the Contributions section is always present.
    /// </summary>
    [Test]
    public async Task FormatReleaseNotes_AlwaysContainsContributionsSection()
    {
        var grouped = new Dictionary<string, List<GitHubCommit>>(StringComparer.OrdinalIgnoreCase);
        var allAuthors = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var newAuthors = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        var result = ReleaseNoteGenerator.FormatReleaseNotes(
            "owner",
            "repo",
            "https://github.com/owner/repo/compare/v1.0...v2.0",
            allAuthors,
            newAuthors,
            grouped);

        await Assert.That(result).Contains("Contributions");
    }

    /// <summary>
    /// Creates a test <see cref="GitHubCommit"/> with the specified message and SHA.
    /// </summary>
    /// <param name="message">The commit message.</param>
    /// <param name="sha">The commit SHA hash.</param>
    /// <returns>A configured <see cref="GitHubCommit"/> for testing.</returns>
    private static GitHubCommit CreateCommit(string message, string sha)
    {
        var gitCommit = new Commit(
            nodeId: null,
            url: null,
            label: null,
            @ref: null,
            sha: sha,
            user: null,
            repository: null,
            message: message,
            author: null,
            committer: null,
            tree: null!,
            parents: [],
            commentCount: 0,
            verification: null);

        return new GitHubCommit(
            nodeId: null,
            url: null,
            label: null,
            @ref: null,
            sha: sha,
            user: null,
            repository: null,
            author: null,
            commentsUrl: null,
            commit: gitCommit,
            committer: null,
            htmlUrl: null,
            stats: null,
            parents: [],
            files: []);
    }
}
