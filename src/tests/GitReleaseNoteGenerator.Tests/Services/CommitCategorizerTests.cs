// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using GitReleaseNoteGenerator.Models;
using GitReleaseNoteGenerator.Services;

namespace GitReleaseNoteGenerator.Tests.Services;

/// <summary>Tests for <see cref="CommitCategorizer"/>.</summary>
public class CommitCategorizerTests
{
    /// <summary>The Features category name.</summary>
    private const string FeaturesCategory = "Features";

    /// <summary>The Fixes category name.</summary>
    private const string FixesCategory = "Fixes";

    /// <summary>The fallback category name for commits that match no prefix.</summary>
    private const string OtherCategory = "Other";

    /// <summary>The Breaking Changes category name.</summary>
    private const string BreakingChangesCategory = "Breaking Changes";

    /// <summary>Tests that a feat-prefixed commit is categorized as Features.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task CategorizeCommitWithFeatPrefixReturnsFeatures()
    {
        var commit = CreateCommit("feat: new button");

        var (_, category) = CommitCategorizer.CategorizeCommit(commit);

        await Assert.That(category).IsEqualTo(FeaturesCategory);
    }

    /// <summary>Tests that a fix-prefixed commit is categorized as Fixes.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task CategorizeCommitWithFixPrefixReturnsFixes()
    {
        var commit = CreateCommit("fix: null reference");

        var (_, category) = CommitCategorizer.CategorizeCommit(commit);

        await Assert.That(category).IsEqualTo(FixesCategory);
    }

    /// <summary>Tests that a commit with no matching prefix is categorized as Other.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task CategorizeCommitWithNoPrefixReturnsOther()
    {
        var commit = CreateCommit("random change");

        var (_, category) = CommitCategorizer.CategorizeCommit(commit);

        await Assert.That(category).IsEqualTo(OtherCategory);
    }

    /// <summary>Tests that dependabot commits are categorized as Dependencies.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task CategorizeCommitWithDependabotLoginReturnsDependencies()
    {
        var commit = CreateCommit("bump package version", authorLogin: "dependabot[bot]");

        var (_, category) = CommitCategorizer.CategorizeCommit(commit);

        await Assert.That(category).IsEqualTo("Dependencies");
    }

    /// <summary>Tests that renovate bot commits are categorized as Dependencies.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task CategorizeCommitWithRenovateLoginReturnsDependencies()
    {
        var commit = CreateCommit("update dependency X", authorLogin: "renovate[bot]");

        var (_, category) = CommitCategorizer.CategorizeCommit(commit);

        await Assert.That(category).IsEqualTo("Dependencies");
    }

    /// <summary>Tests that a scoped conventional commit is categorized by its type.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task CategorizeCommitWithScopeReturnsTypeCategory()
    {
        var commit = CreateCommit("feat(api): add endpoint");

        var (_, category) = CommitCategorizer.CategorizeCommit(commit);

        await Assert.That(category).IsEqualTo(FeaturesCategory);
    }

    /// <summary>Tests that a "!" breaking marker promotes the commit to Breaking Changes.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task CategorizeCommitWithBreakingBangReturnsBreakingChanges()
    {
        var commit = CreateCommit("feat!: drop legacy API");

        var (_, category) = CommitCategorizer.CategorizeCommit(commit);

        await Assert.That(category).IsEqualTo(BreakingChangesCategory);
    }

    /// <summary>Tests that a scoped "!" breaking marker promotes the commit to Breaking Changes.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task CategorizeCommitWithScopedBreakingBangReturnsBreakingChanges()
    {
        var commit = CreateCommit("refactor(core)!: rework pipeline");

        var (_, category) = CommitCategorizer.CategorizeCommit(commit);

        await Assert.That(category).IsEqualTo(BreakingChangesCategory);
    }

    /// <summary>Tests that a BREAKING CHANGE footer promotes the commit to Breaking Changes.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task CategorizeCommitWithBreakingChangeFooterReturnsBreakingChanges()
    {
        var commit = CreateCommit("feat: add option\n\nBREAKING CHANGE: config format changed");

        var (_, category) = CommitCategorizer.CategorizeCommit(commit);

        await Assert.That(category).IsEqualTo(BreakingChangesCategory);
    }

    /// <summary>Tests that the conventional "ci" type is recognized.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task CategorizeCommitWithCiTypeReturnsGeneralChanges()
    {
        var commit = CreateCommit("ci: update workflow");

        var (_, category) = CommitCategorizer.CategorizeCommit(commit);

        await Assert.That(category).IsEqualTo("General Changes");
    }

    /// <summary>Tests that the conventional "revert" type is recognized.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task CategorizeCommitWithRevertTypeReturnsGeneralChanges()
    {
        var commit = CreateCommit("revert: undo bad change");

        var (_, category) = CommitCategorizer.CategorizeCommit(commit);

        await Assert.That(category).IsEqualTo("General Changes");
    }

    /// <summary>
    /// Tests that a non-conventional message that merely starts with a type word is not
    /// misclassified (no colon means it is not a conventional commit).
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task CategorizeCommitWithNonConventionalFixWordReturnsOther()
    {
        var commit = CreateCommit("fixture cleanup for tests");

        var (_, category) = CommitCategorizer.CategorizeCommit(commit);

        await Assert.That(category).IsEqualTo(OtherCategory);
    }

    /// <summary>Tests that a conventional commit with an unrecognized type falls back to Other.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task CategorizeCommitWithUnknownTypeReturnsOther()
    {
        var commit = CreateCommit("wip: still working");

        var (_, category) = CommitCategorizer.CategorizeCommit(commit);

        await Assert.That(category).IsEqualTo(OtherCategory);
    }

    /// <summary>Tests that GroupByCategory returns commits grouped by priority order.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task GroupByCategoryReturnsGroupedByPriority()
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

    /// <summary>Tests that GetEmoji returns the correct emoji for known categories.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task GetEmojiWithKnownCategoryReturnsEmoji()
    {
        var emoji = CommitCategorizer.GetEmoji(FeaturesCategory);

        await Assert.That(emoji).IsEqualTo("\u2728");
    }

    /// <summary>Tests that GetEmoji returns a fallback for unknown categories.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task GetEmojiWithUnknownCategoryReturnsFallback()
    {
        var emoji = CommitCategorizer.GetEmoji("NonExistent");

        await Assert.That(emoji).IsEqualTo("\U0001f539");
    }

    /// <summary>Creates a test <see cref="GitHubCommit"/> with the specified message and optional author login.</summary>
    /// <param name="message">The commit message.</param>
    /// <param name="authorLogin">The GitHub login of the author, or null.</param>
    /// <returns>A configured <see cref="GitHubCommit"/> for testing.</returns>
    private static GitHubCommit CreateCommit(string message, string? authorLogin = null) =>
        new(
            "abc123",
            new GitCommitDetail(message, Author: null, Committer: null),
            authorLogin is not null ? new GitHubUser(authorLogin) : null,
            Committer: null);
}
