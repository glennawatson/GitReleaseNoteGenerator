// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using GitReleaseNoteGenerator.Models;
using GitReleaseNoteGenerator.Services;

namespace GitReleaseNoteGenerator.Tests.Services;

/// <summary>
/// Tests for <see cref="AuthorExtractor"/>.
/// </summary>
public class AuthorExtractorTests
{
    /// <summary>
    /// A sample GitHub login used across the extraction tests.
    /// </summary>
    private const string Octocat = "octocat";

    /// <summary>
    /// A sample GitHub login used to verify co-author de-duplication.
    /// </summary>
    private const string GlennLogin = "glennawatson";

    /// <summary>
    /// Tests that the primary author login is extracted.
    /// </summary>
    [Test]
    public async Task GetCommitAuthors_WithAuthorLogin_ExtractsLogin()
    {
        var commit = CreateCommit("some message", authorLogin: Octocat);

        var authors = AuthorExtractor.GetCommitAuthors(commit);

        await Assert.That(authors).Contains(Octocat);
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
        const string Message = "feat: add feature\n\nCo-authored-by: Jane <jane@example.com>\nCo-authored-by: Bob <bob@example.com>";
        var commit = CreateCommit(Message, authorLogin: Octocat);

        var authors = AuthorExtractor.GetCommitAuthors(commit);

        await Assert.That(authors).Contains(Octocat);
        await Assert.That(authors).Contains("Jane");
        await Assert.That(authors).Contains("Bob");
    }

    /// <summary>
    /// Tests that co-author lines with leading whitespace are correctly trimmed.
    /// </summary>
    [Test]
    public async Task GetCommitAuthors_WithIndentedCoAuthor_TrimsAndExtracts()
    {
        const string Message = "fix: something\n\n  Co-authored-by: Alice <alice@example.com>";
        var commit = CreateCommit(Message, authorLogin: Octocat);

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
    /// Tests that a GitHub noreply email with a numeric ID prefix yields the embedded login.
    /// </summary>
    [Test]
    public async Task TryGetLoginFromNoReplyEmail_WithNumericPrefix_ReturnsLogin()
    {
        var login = AuthorExtractor.TryGetLoginFromNoReplyEmail("12345+glennawatson@users.noreply.github.com");

        await Assert.That(login).IsEqualTo(GlennLogin);
    }

    /// <summary>
    /// Tests that a legacy GitHub noreply email without an ID prefix yields the login.
    /// </summary>
    [Test]
    public async Task TryGetLoginFromNoReplyEmail_WithoutPrefix_ReturnsLogin()
    {
        var login = AuthorExtractor.TryGetLoginFromNoReplyEmail("glennawatson@users.noreply.github.com");

        await Assert.That(login).IsEqualTo(GlennLogin);
    }

    /// <summary>
    /// Tests that a regular (non-noreply) email yields no login.
    /// </summary>
    [Test]
    public async Task TryGetLoginFromNoReplyEmail_WithRegularEmail_ReturnsNull()
    {
        var login = AuthorExtractor.TryGetLoginFromNoReplyEmail("glenn@glennwatson.net");

        await Assert.That(login).IsNull();
    }

    /// <summary>
    /// Tests that a co-author whose noreply email embeds the same login as the primary author
    /// collapses to a single contributor rather than appearing as a separate display name.
    /// </summary>
    [Test]
    public async Task GetCommitAuthors_WithNoReplyCoAuthorMatchingPrimary_CollapsesToSingleLogin()
    {
        const string Message = "feat: add feature\n\nCo-authored-by: Glenn Watson <12345+glennawatson@users.noreply.github.com>";
        var commit = CreateCommit(Message, authorLogin: GlennLogin);

        var authors = AuthorExtractor.GetCommitAuthors(commit);

        await Assert.That(authors).Contains(GlennLogin);
        await Assert.That(authors.Count).IsEqualTo(1);
    }

    /// <summary>
    /// Tests that a co-author trailer without an &lt;email&gt; falls back to the display name.
    /// </summary>
    [Test]
    public async Task GetCommitAuthors_WithCoAuthorWithoutEmail_UsesName()
    {
        const string Message = "feat: add feature\n\nCo-authored-by: Jane Doe";
        var commit = CreateCommit(Message, authorLogin: Octocat);

        var authors = AuthorExtractor.GetCommitAuthors(commit);

        await Assert.That(authors).Contains("JaneDoe");
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
    public async Task IsBot_WithRegularUser_ReturnsFalse() =>
        await Assert.That(AuthorExtractor.IsBot(Octocat)).IsFalse();

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
        var signature = commitAuthorName is not null
            ? new GitSignature(commitAuthorName, commitAuthorName + "@test.com")
            : null;

        return new GitHubCommit(
            "abc123",
            new GitCommitDetail(message, signature, signature),
            authorLogin is not null ? new GitHubUser(authorLogin) : null,
            Committer: null);
    }
}
