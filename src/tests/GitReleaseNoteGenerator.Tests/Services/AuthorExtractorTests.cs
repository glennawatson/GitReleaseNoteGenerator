// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using GitReleaseNoteGenerator.Models;
using GitReleaseNoteGenerator.Services;

namespace GitReleaseNoteGenerator.Tests.Services;

/// <summary>Tests for <see cref="AuthorExtractor"/>.</summary>
public class AuthorExtractorTests
{
    /// <summary>A sample GitHub login used across the extraction tests.</summary>
    private const string Octocat = "octocat";

    /// <summary>A sample GitHub login used to verify co-author de-duplication.</summary>
    private const string TestUserLogin = "testuser";

    /// <summary>The identifier returned when no usable author information is present.</summary>
    private const string UnknownAuthor = "unknown";

    /// <summary>A display name with no matching GitHub login, used for the name-fallback tests.</summary>
    private const string DisplayName = "John Doe";

    /// <summary>Tests that the primary author login is extracted.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task GetCommitAuthorsWithAuthorLoginExtractsLogin()
    {
        var commit = CreateCommit("some message", authorLogin: Octocat);

        var authors = AuthorExtractor.GetCommitAuthors(commit);

        await Assert.That(Values(authors)).Contains(Octocat);
    }

    /// <summary>Tests fallback to committer name when no login is available.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task GetCommitAuthorsWithNoLoginFallsBackToCommitAuthorName()
    {
        var commit = CreateCommit("some message", commitAuthorName: DisplayName);

        var authors = AuthorExtractor.GetCommitAuthors(commit);

        await Assert.That(Values(authors)).Contains(DisplayName);
    }

    /// <summary>Tests that co-authors are extracted from commit message trailers.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task GetCommitAuthorsWithCoAuthorsExtractsAll()
    {
        const string Message = "feat: add feature\n\nCo-authored-by: Jane <jane@example.com>\nCo-authored-by: Bob <bob@example.com>";
        var commit = CreateCommit(Message, authorLogin: Octocat);

        var authors = AuthorExtractor.GetCommitAuthors(commit);

        await Assert.That(Values(authors)).Contains(Octocat);
        await Assert.That(Values(authors)).Contains("Jane");
        await Assert.That(Values(authors)).Contains("Bob");
    }

    /// <summary>Tests that co-author lines with leading whitespace are correctly trimmed.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task GetCommitAuthorsWithIndentedCoAuthorTrimsAndExtracts()
    {
        const string Message = "fix: something\n\n  Co-authored-by: Alice <alice@example.com>";
        var commit = CreateCommit(Message, authorLogin: Octocat);

        var authors = AuthorExtractor.GetCommitAuthors(commit);

        await Assert.That(Values(authors)).Contains("Alice");
    }

    /// <summary>Tests that NormalizeAuthorName strips the email and keeps the readable display name.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task NormalizeAuthorNameWithEmailFormatStripsEmail()
    {
        var result = AuthorExtractor.NormalizeAuthorName($"{DisplayName} <john@example.com>");

        await Assert.That(result).IsEqualTo(DisplayName);
    }

    /// <summary>Tests that NormalizeAuthorName reduces runs of whitespace to single spaces.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task NormalizeAuthorNameWithRepeatedWhitespaceCollapsesToSingleSpaces()
    {
        var result = AuthorExtractor.NormalizeAuthorName("  Tést   Üser \t dé Éxample  ");

        await Assert.That(result).IsEqualTo("Tést Üser dé Éxample");
    }

    /// <summary>Tests that NormalizeAuthorName returns unknown for empty strings.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task NormalizeAuthorNameWithEmptyReturnsUnknown()
    {
        var result = AuthorExtractor.NormalizeAuthorName(string.Empty);

        await Assert.That(result).IsEqualTo(UnknownAuthor);
    }

    /// <summary>Tests that NormalizeAuthorName returns unknown for whitespace-only strings.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task NormalizeAuthorNameWithWhitespaceReturnsUnknown()
    {
        var result = AuthorExtractor.NormalizeAuthorName("   ");

        await Assert.That(result).IsEqualTo(UnknownAuthor);
    }

    /// <summary>Tests that NormalizeAuthorName returns unknown when only email remains after stripping.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task NormalizeAuthorNameWithOnlyEmailReturnsUnknown()
    {
        var result = AuthorExtractor.NormalizeAuthorName("<noreply@github.com>");

        await Assert.That(result).IsEqualTo(UnknownAuthor);
    }

    /// <summary>Tests that a GitHub noreply email with a numeric ID prefix yields the embedded login.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task TryGetLoginFromNoReplyEmailWithNumericPrefixReturnsLogin()
    {
        var login = AuthorExtractor.TryGetLoginFromNoReplyEmail($"12345+{TestUserLogin}@users.noreply.github.com");

        await Assert.That(login).IsEqualTo(TestUserLogin);
    }

    /// <summary>Tests that a legacy GitHub noreply email without an ID prefix yields the login.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task TryGetLoginFromNoReplyEmailWithoutPrefixReturnsLogin()
    {
        var login = AuthorExtractor.TryGetLoginFromNoReplyEmail($"{TestUserLogin}@users.noreply.github.com");

        await Assert.That(login).IsEqualTo(TestUserLogin);
    }

    /// <summary>Tests that a regular (non-noreply) email yields no login.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task TryGetLoginFromNoReplyEmailWithRegularEmailReturnsNull()
    {
        var login = AuthorExtractor.TryGetLoginFromNoReplyEmail("testuser@example.com");

        await Assert.That(login).IsNull();
    }

    /// <summary>
    /// Tests that a co-author whose noreply email embeds the same login as the primary author
    /// collapses to a single contributor rather than appearing as a separate display name.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task GetCommitAuthorsWithNoReplyCoAuthorMatchingPrimaryCollapsesToSingleLogin()
    {
        const string Message = "feat: add feature\n\nCo-authored-by: Test User <12345+testuser@users.noreply.github.com>";
        var commit = CreateCommit(Message, authorLogin: TestUserLogin);

        var authors = AuthorExtractor.GetCommitAuthors(commit);

        await Assert.That(Values(authors)).Contains(TestUserLogin);
        await Assert.That(authors.Count).IsEqualTo(1);
    }

    /// <summary>Tests that a co-author trailer without an &lt;email&gt; falls back to the display name.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task GetCommitAuthorsWithCoAuthorWithoutEmailUsesName()
    {
        const string Message = "feat: add feature\n\nCo-authored-by: Jane Doe";
        var commit = CreateCommit(Message, authorLogin: Octocat);

        var authors = AuthorExtractor.GetCommitAuthors(commit);

        await Assert.That(Values(authors)).Contains("Jane Doe");
    }

    /// <summary>Tests bot detection.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task IsBotWithBotSuffixReturnsTrue()
    {
        await Assert.That(AuthorExtractor.IsBot("dependabot[bot]")).IsTrue();
        await Assert.That(AuthorExtractor.IsBot("renovate[bot]")).IsTrue();
    }

    /// <summary>Tests that non-bot users are not detected as bots.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task IsBotWithRegularUserReturnsFalse() =>
        await Assert.That(AuthorExtractor.IsBot(Octocat)).IsFalse();

    /// <summary>Projects resolved identities down to their identifier values for assertion.</summary>
    /// <param name="authors">The resolved contributor identities.</param>
    /// <returns>The identifier values, in order.</returns>
    private static List<string> Values(IEnumerable<ContributorIdentity> authors)
    {
        var values = new List<string>();
        foreach (var author in authors)
        {
            values.Add(author.Value);
        }

        return values;
    }

    /// <summary>Creates a test <see cref="GitHubCommit"/> with the specified message and optional author details.</summary>
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
            ? new GitSignature(commitAuthorName, $"{commitAuthorName}@test.com")
            : null;

        return new(
            "abc123",
            new GitCommitDetail(message, signature, signature),
            authorLogin is not null ? new GitHubUser(authorLogin) : null,
            Committer: null);
    }
}
