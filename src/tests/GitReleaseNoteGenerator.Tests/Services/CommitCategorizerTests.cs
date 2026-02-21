// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using GitReleaseNoteGenerator.Services;

using Octokit;

namespace GitReleaseNoteGenerator.Tests.Services;

/// <summary>
/// Tests for <see cref="CommitCategorizer"/>.
/// </summary>
public class CommitCategorizerTests
{
    /// <summary>
    /// Tests that a feat-prefixed commit is categorized as Features.
    /// </summary>
    [Test]
    public async Task CategorizeCommit_WithFeatPrefix_ReturnsFeatures()
    {
        var commit = CreateCommit("feat: new button");

        var (_, category) = CommitCategorizer.CategorizeCommit(commit);

        await Assert.That(category).IsEqualTo("Features");
    }

    /// <summary>
    /// Tests that a fix-prefixed commit is categorized as Fixes.
    /// </summary>
    [Test]
    public async Task CategorizeCommit_WithFixPrefix_ReturnsFixes()
    {
        var commit = CreateCommit("fix: null reference");

        var (_, category) = CommitCategorizer.CategorizeCommit(commit);

        await Assert.That(category).IsEqualTo("Fixes");
    }

    /// <summary>
    /// Tests that a commit with no matching prefix is categorized as Other.
    /// </summary>
    [Test]
    public async Task CategorizeCommit_WithNoPrefix_ReturnsOther()
    {
        var commit = CreateCommit("random change");

        var (_, category) = CommitCategorizer.CategorizeCommit(commit);

        await Assert.That(category).IsEqualTo("Other");
    }

    /// <summary>
    /// Tests that dependabot commits are categorized as Dependencies.
    /// </summary>
    [Test]
    public async Task CategorizeCommit_WithDependabotLogin_ReturnsDependencies()
    {
        var commit = CreateCommit("bump package version", authorLogin: "dependabot[bot]");

        var (_, category) = CommitCategorizer.CategorizeCommit(commit);

        await Assert.That(category).IsEqualTo("Dependencies");
    }

    /// <summary>
    /// Tests that renovate bot commits are categorized as Dependencies.
    /// </summary>
    [Test]
    public async Task CategorizeCommit_WithRenovateLogin_ReturnsDependencies()
    {
        var commit = CreateCommit("update dependency X", authorLogin: "renovate[bot]");

        var (_, category) = CommitCategorizer.CategorizeCommit(commit);

        await Assert.That(category).IsEqualTo("Dependencies");
    }

    /// <summary>
    /// Tests that GroupByCategory returns commits grouped by priority order.
    /// </summary>
    [Test]
    public async Task GroupByCategory_ReturnsGroupedByPriority()
    {
        var commits = new[]
        {
            CreateCommit("fix: something"),
            CreateCommit("feat: new thing"),
            CreateCommit("fix: another fix"),
        };

        var grouped = CommitCategorizer.GroupByCategory(commits);

        await Assert.That(grouped).ContainsKey("Features");
        await Assert.That(grouped).ContainsKey("Fixes");
        await Assert.That(grouped["Fixes"]).Count().IsEqualTo(2);
        await Assert.That(grouped["Features"]).Count().IsEqualTo(1);
    }

    /// <summary>
    /// Tests that GetEmoji returns the correct emoji for known categories.
    /// </summary>
    [Test]
    public async Task GetEmoji_WithKnownCategory_ReturnsEmoji()
    {
        var emoji = CommitCategorizer.GetEmoji("Features");

        await Assert.That(emoji).IsEqualTo("\u2728");
    }

    /// <summary>
    /// Tests that GetEmoji returns a fallback for unknown categories.
    /// </summary>
    [Test]
    public async Task GetEmoji_WithUnknownCategory_ReturnsFallback()
    {
        var emoji = CommitCategorizer.GetEmoji("NonExistent");

        await Assert.That(emoji).IsEqualTo("\U0001f539");
    }

    /// <summary>
    /// Creates a test <see cref="GitHubCommit"/> with the specified message and optional author login.
    /// </summary>
    /// <param name="message">The commit message.</param>
    /// <param name="authorLogin">The GitHub login of the author, or null.</param>
    /// <returns>A configured <see cref="GitHubCommit"/> for testing.</returns>
    private static GitHubCommit CreateCommit(string message, string? authorLogin = null)
    {
        var gitCommit = new Commit(
            nodeId: null,
            url: null,
            label: null,
            @ref: null,
            sha: "abc123",
            user: null,
            repository: null,
            message: message,
            author: null,
            committer: null,
            tree: null!,
            parents: [],
            commentCount: 0,
            verification: null);

        Author? author = authorLogin is not null
            ? new Author(
                login: authorLogin,
                id: 1,
                nodeId: null,
                avatarUrl: null,
                url: null,
                htmlUrl: null,
                followersUrl: null,
                followingUrl: null,
                gistsUrl: null,
                type: "User",
                starredUrl: null,
                subscriptionsUrl: null,
                organizationsUrl: null,
                reposUrl: null,
                eventsUrl: null,
                receivedEventsUrl: null,
                siteAdmin: false)
            : null;

        return new GitHubCommit(
            nodeId: null,
            url: null,
            label: null,
            @ref: null,
            sha: "abc123",
            user: null,
            repository: null,
            author: author,
            commentsUrl: null,
            commit: gitCommit,
            committer: null,
            htmlUrl: null,
            stats: null,
            parents: [],
            files: []);
    }
}
