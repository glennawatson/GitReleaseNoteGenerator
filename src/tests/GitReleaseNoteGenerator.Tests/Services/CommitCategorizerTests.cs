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
    /// The Features category name.
    /// </summary>
    private const string FeaturesCategory = "Features";

    /// <summary>
    /// Tests that a feat-prefixed commit is categorized as Features.
    /// </summary>
    [Test]
    public async Task CategorizeCommit_WithFeatPrefix_ReturnsFeatures()
    {
        var commit = CreateCommit("feat: new button");

        var (_, category) = CommitCategorizer.CategorizeCommit(commit);

        await Assert.That(category).IsEqualTo(FeaturesCategory);
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
    /// Tests that a scoped conventional commit is categorized by its type.
    /// </summary>
    [Test]
    public async Task CategorizeCommit_WithScope_ReturnsTypeCategory()
    {
        var commit = CreateCommit("feat(api): add endpoint");

        var (_, category) = CommitCategorizer.CategorizeCommit(commit);

        await Assert.That(category).IsEqualTo(FeaturesCategory);
    }

    /// <summary>
    /// Tests that a "!" breaking marker promotes the commit to Breaking Changes.
    /// </summary>
    [Test]
    public async Task CategorizeCommit_WithBreakingBang_ReturnsBreakingChanges()
    {
        var commit = CreateCommit("feat!: drop legacy API");

        var (_, category) = CommitCategorizer.CategorizeCommit(commit);

        await Assert.That(category).IsEqualTo("Breaking Changes");
    }

    /// <summary>
    /// Tests that a scoped "!" breaking marker promotes the commit to Breaking Changes.
    /// </summary>
    [Test]
    public async Task CategorizeCommit_WithScopedBreakingBang_ReturnsBreakingChanges()
    {
        var commit = CreateCommit("refactor(core)!: rework pipeline");

        var (_, category) = CommitCategorizer.CategorizeCommit(commit);

        await Assert.That(category).IsEqualTo("Breaking Changes");
    }

    /// <summary>
    /// Tests that a BREAKING CHANGE footer promotes the commit to Breaking Changes.
    /// </summary>
    [Test]
    public async Task CategorizeCommit_WithBreakingChangeFooter_ReturnsBreakingChanges()
    {
        var commit = CreateCommit("feat: add option\n\nBREAKING CHANGE: config format changed");

        var (_, category) = CommitCategorizer.CategorizeCommit(commit);

        await Assert.That(category).IsEqualTo("Breaking Changes");
    }

    /// <summary>
    /// Tests that the conventional "ci" type is recognized.
    /// </summary>
    [Test]
    public async Task CategorizeCommit_WithCiType_ReturnsGeneralChanges()
    {
        var commit = CreateCommit("ci: update workflow");

        var (_, category) = CommitCategorizer.CategorizeCommit(commit);

        await Assert.That(category).IsEqualTo("General Changes");
    }

    /// <summary>
    /// Tests that the conventional "revert" type is recognized.
    /// </summary>
    [Test]
    public async Task CategorizeCommit_WithRevertType_ReturnsGeneralChanges()
    {
        var commit = CreateCommit("revert: undo bad change");

        var (_, category) = CommitCategorizer.CategorizeCommit(commit);

        await Assert.That(category).IsEqualTo("General Changes");
    }

    /// <summary>
    /// Tests that a non-conventional message that merely starts with a type word is not
    /// misclassified (no colon means it is not a conventional commit).
    /// </summary>
    [Test]
    public async Task CategorizeCommit_WithNonConventionalFixWord_ReturnsOther()
    {
        var commit = CreateCommit("fixture cleanup for tests");

        var (_, category) = CommitCategorizer.CategorizeCommit(commit);

        await Assert.That(category).IsEqualTo("Other");
    }

    /// <summary>
    /// Tests that a conventional commit with an unrecognized type falls back to Other.
    /// </summary>
    [Test]
    public async Task CategorizeCommit_WithUnknownType_ReturnsOther()
    {
        var commit = CreateCommit("wip: still working");

        var (_, category) = CommitCategorizer.CategorizeCommit(commit);

        await Assert.That(category).IsEqualTo("Other");
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
            CreateCommit("fix: another fix")
        };

        var grouped = CommitCategorizer.GroupByCategory(commits);

        const int ExpectedFixCount = 2;

        await Assert.That(grouped).ContainsKey(FeaturesCategory);
        await Assert.That(grouped).ContainsKey("Fixes");
        await Assert.That(grouped["Fixes"]).Count().IsEqualTo(ExpectedFixCount);
        await Assert.That(grouped[FeaturesCategory]).Count().IsEqualTo(1);
    }

    /// <summary>
    /// Tests that GetEmoji returns the correct emoji for known categories.
    /// </summary>
    [Test]
    public async Task GetEmoji_WithKnownCategory_ReturnsEmoji()
    {
        var emoji = CommitCategorizer.GetEmoji(FeaturesCategory);

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

        var author = authorLogin is not null
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

        return new(
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
