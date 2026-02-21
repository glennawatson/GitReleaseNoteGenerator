// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using GitReleaseNoteGenerator.Services;

using Octokit;

namespace GitReleaseNoteGenerator.Tests.Services;

/// <summary>
/// Tests for <see cref="AuthorExtractor"/>.
/// </summary>
public class AuthorExtractorTests
{
    /// <summary>
    /// Tests that the primary author login is extracted.
    /// </summary>
    [Test]
    public async Task GetCommitAuthors_WithAuthorLogin_ExtractsLogin()
    {
        var commit = CreateCommit("some message", authorLogin: "octocat");

        var authors = AuthorExtractor.GetCommitAuthors(commit);

        await Assert.That(authors).Contains("octocat");
    }

    /// <summary>
    /// Tests fallback to committer name when no login is available.
    /// </summary>
    [Test]
    public async Task GetCommitAuthors_WithNoLogin_FallsBackToCommitAuthorName()
    {
        var commit = CreateCommit("some message", commitAuthorName: "John Doe");

        var authors = AuthorExtractor.GetCommitAuthors(commit);

        await Assert.That(authors).Contains("JohnDoe");
    }

    /// <summary>
    /// Tests that co-authors are extracted from commit message trailers.
    /// </summary>
    [Test]
    public async Task GetCommitAuthors_WithCoAuthors_ExtractsAll()
    {
        var message = "feat: add feature\n\nCo-authored-by: Jane <jane@example.com>\nCo-authored-by: Bob <bob@example.com>";
        var commit = CreateCommit(message, authorLogin: "octocat");

        var authors = AuthorExtractor.GetCommitAuthors(commit);

        await Assert.That(authors).Contains("octocat");
        await Assert.That(authors).Contains("Jane");
        await Assert.That(authors).Contains("Bob");
    }

    /// <summary>
    /// Tests that co-author lines with leading whitespace are correctly trimmed.
    /// </summary>
    [Test]
    public async Task GetCommitAuthors_WithIndentedCoAuthor_TrimsAndExtracts()
    {
        var message = "fix: something\n\n  Co-authored-by: Alice <alice@example.com>";
        var commit = CreateCommit(message, authorLogin: "octocat");

        var authors = AuthorExtractor.GetCommitAuthors(commit);

        await Assert.That(authors).Contains("Alice");
    }

    /// <summary>
    /// Tests that NormalizeAuthorName strips email and whitespace.
    /// </summary>
    [Test]
    public async Task NormalizeAuthorName_WithEmailFormat_StripsEmail()
    {
        var result = AuthorExtractor.NormalizeAuthorName("John Doe <john@example.com>");

        await Assert.That(result).IsEqualTo("JohnDoe");
    }

    /// <summary>
    /// Tests that NormalizeAuthorName returns unknown for empty strings.
    /// </summary>
    [Test]
    public async Task NormalizeAuthorName_WithEmpty_ReturnsUnknown()
    {
        var result = AuthorExtractor.NormalizeAuthorName(string.Empty);

        await Assert.That(result).IsEqualTo("unknown");
    }

    /// <summary>
    /// Tests that NormalizeAuthorName returns unknown for whitespace-only strings.
    /// </summary>
    [Test]
    public async Task NormalizeAuthorName_WithWhitespace_ReturnsUnknown()
    {
        var result = AuthorExtractor.NormalizeAuthorName("   ");

        await Assert.That(result).IsEqualTo("unknown");
    }

    /// <summary>
    /// Tests that NormalizeAuthorName returns unknown when only email remains after stripping.
    /// </summary>
    [Test]
    public async Task NormalizeAuthorName_WithOnlyEmail_ReturnsUnknown()
    {
        var result = AuthorExtractor.NormalizeAuthorName("<noreply@github.com>");

        await Assert.That(result).IsEqualTo("unknown");
    }

    /// <summary>
    /// Tests bot detection.
    /// </summary>
    [Test]
    public async Task IsBot_WithBotSuffix_ReturnsTrue()
    {
        await Assert.That(AuthorExtractor.IsBot("dependabot[bot]")).IsTrue();
        await Assert.That(AuthorExtractor.IsBot("renovate[bot]")).IsTrue();
    }

    /// <summary>
    /// Tests that non-bot users are not detected as bots.
    /// </summary>
    [Test]
    public async Task IsBot_WithRegularUser_ReturnsFalse()
    {
        await Assert.That(AuthorExtractor.IsBot("octocat")).IsFalse();
    }

    /// <summary>
    /// Creates a test <see cref="GitHubCommit"/> with the specified message and optional author details.
    /// </summary>
    /// <param name="message">The commit message.</param>
    /// <param name="authorLogin">The GitHub login of the author, or null.</param>
    /// <param name="commitAuthorName">The git commit author name, or null.</param>
    /// <returns>A configured <see cref="GitHubCommit"/> for testing.</returns>
    private static GitHubCommit CreateCommit(
        string message,
        string? authorLogin = null,
        string? commitAuthorName = null)
    {
        var commitAuthor = commitAuthorName is not null
            ? new Committer(commitAuthorName, commitAuthorName + "@test.com", DateTimeOffset.Now)
            : null;

        var gitCommit = new Commit(
            nodeId: null,
            url: null,
            label: null,
            @ref: null,
            sha: "abc123",
            user: null,
            repository: null,
            message: message,
            author: commitAuthor,
            committer: commitAuthor,
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
