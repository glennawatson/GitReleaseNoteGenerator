// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using GitReleaseNoteGenerator.Models;
using GitReleaseNoteGenerator.Services;

using Microsoft.Extensions.Logging.Abstractions;

using Octokit;

namespace GitReleaseNoteGenerator.Tests.Services;

/// <summary>
/// Tests for <see cref="AuthorResolver"/>, exercising the email-to-login API tier through a
/// hand-rolled <see cref="IUserLoginSearch"/> test double (no mocking framework).
/// </summary>
public class AuthorResolverTests
{
    /// <summary>
    /// A contributor display name used across the resolution tests.
    /// </summary>
    private const string GlennName = "Glenn Watson";

    /// <summary>
    /// The GitHub login that the display name and email resolve to.
    /// </summary>
    private const string GlennLogin = "glennawatson";

    /// <summary>
    /// A real (non-noreply) email used across the resolution tests.
    /// </summary>
    private const string GlennEmail = "glenn@glennwatson.net";

    /// <summary>
    /// Tests that an already-resolved login is returned without querying the search API.
    /// </summary>
    [Test]
    public async Task ResolveAsync_WithExistingLogin_DoesNotQuerySearch()
    {
        var search = new FakeUserLoginSearch();
        var resolver = new AuthorResolver(search, NullLogger.Instance);

        var result = await resolver.ResolveAsync(new("octocat", "Octo Cat", "octo@example.com"));

        await Assert.That(result).IsEqualTo("octocat");
        await Assert.That(search.CallCount).IsEqualTo(0);
    }

    /// <summary>
    /// Tests that a GitHub noreply email is resolved locally without querying the search API.
    /// </summary>
    [Test]
    public async Task ResolveAsync_WithNoReplyEmail_DoesNotQuerySearch()
    {
        var search = new FakeUserLoginSearch();
        var resolver = new AuthorResolver(search, NullLogger.Instance);

        var result = await resolver.ResolveAsync(
            new(null, GlennName, "12345+glennawatson@users.noreply.github.com"));

        await Assert.That(result).IsEqualTo(GlennLogin);
        await Assert.That(search.CallCount).IsEqualTo(0);
    }

    /// <summary>
    /// Tests that a real email is resolved to a login via the search API.
    /// </summary>
    [Test]
    public async Task ResolveAsync_WithRealEmail_ReturnsLoginFromSearch()
    {
        var search = new FakeUserLoginSearch(new() { [GlennEmail] = GlennLogin });
        var resolver = new AuthorResolver(search, NullLogger.Instance);

        var result = await resolver.ResolveAsync(new(null, GlennName, GlennEmail));

        await Assert.That(result).IsEqualTo(GlennLogin);
        await Assert.That(search.CallCount).IsEqualTo(1);
    }

    /// <summary>
    /// Tests that an unresolvable email falls back to the normalized display name.
    /// </summary>
    [Test]
    public async Task ResolveAsync_WithUnresolvableEmail_FallsBackToName()
    {
        var search = new FakeUserLoginSearch();
        var resolver = new AuthorResolver(search, NullLogger.Instance);

        var result = await resolver.ResolveAsync(new(null, GlennName, GlennEmail));

        await Assert.That(result).IsEqualTo("GlennWatson");
    }

    /// <summary>
    /// Tests that a successful lookup is cached so the search API is queried only once.
    /// </summary>
    [Test]
    public async Task ResolveAsync_CachesSuccessfulLookup()
    {
        var search = new FakeUserLoginSearch(new() { [GlennEmail] = GlennLogin });
        var resolver = new AuthorResolver(search, NullLogger.Instance);
        var contributor = new CommitContributor(null, GlennName, GlennEmail);

        await resolver.ResolveAsync(contributor);
        await resolver.ResolveAsync(contributor);

        await Assert.That(search.CallCount).IsEqualTo(1);
    }

    /// <summary>
    /// Tests that an unsuccessful lookup is cached so the search API is not queried again.
    /// </summary>
    [Test]
    public async Task ResolveAsync_CachesNegativeLookup()
    {
        var search = new FakeUserLoginSearch();
        var resolver = new AuthorResolver(search, NullLogger.Instance);
        var contributor = new CommitContributor(null, GlennName, GlennEmail);

        await resolver.ResolveAsync(contributor);
        await resolver.ResolveAsync(contributor);

        await Assert.That(search.CallCount).IsEqualTo(1);
    }

    /// <summary>
    /// Tests that an API failure is swallowed and resolution falls back to the display name.
    /// </summary>
    [Test]
    public async Task ResolveAsync_WhenSearchThrows_FallsBackToName()
    {
        var search = new FakeUserLoginSearch(throwApiException: true);
        var resolver = new AuthorResolver(search, NullLogger.Instance);

        var result = await resolver.ResolveAsync(new(null, GlennName, GlennEmail));

        await Assert.That(result).IsEqualTo("GlennWatson");
    }

    /// <summary>
    /// Tests that a primary author and a co-author whose email resolves to the same login
    /// collapse into a single contributor.
    /// </summary>
    [Test]
    public async Task GetResolvedAuthorsAsync_WithApiResolvedCoAuthor_CollapsesToSingleLogin()
    {
        var search = new FakeUserLoginSearch(new() { [GlennEmail] = GlennLogin });
        var resolver = new AuthorResolver(search, NullLogger.Instance);
        var commit = CreateCommit(
            "feat: add feature\n\nCo-authored-by: Glenn Watson <glenn@glennwatson.net>",
            authorLogin: GlennLogin);

        var authors = await resolver.GetResolvedAuthorsAsync(commit);

        await Assert.That(authors.Count).IsEqualTo(1);
        await Assert.That(authors).Contains(GlennLogin);
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

    /// <summary>
    /// A hand-rolled <see cref="IUserLoginSearch"/> test double that returns canned results
    /// and records how many times it was queried.
    /// </summary>
    private sealed class FakeUserLoginSearch : IUserLoginSearch
    {
        /// <summary>
        /// The configured email-to-login responses.
        /// </summary>
        private readonly Dictionary<string, string?> _responses;

        /// <summary>
        /// Whether each lookup should throw an <see cref="ApiException"/>.
        /// </summary>
        private readonly bool _throwApiException;

        /// <summary>
        /// Initializes a new instance of the <see cref="FakeUserLoginSearch"/> class.
        /// </summary>
        /// <param name="responses">The configured email-to-login responses, or null for none.</param>
        /// <param name="throwApiException">Whether each lookup should throw an API exception.</param>
        public FakeUserLoginSearch(Dictionary<string, string?>? responses = null, bool throwApiException = false)
        {
            _responses = responses ?? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            _throwApiException = throwApiException;
        }

        /// <summary>
        /// Gets the number of times the search was queried.
        /// </summary>
        public int CallCount { get; private set; }

        /// <inheritdoc/>
        public Task<string?> FindLoginByEmailAsync(string email)
        {
            CallCount++;
            if (_throwApiException)
            {
                throw new ApiException();
            }

            _responses.TryGetValue(email, out var login);
            return Task.FromResult(login);
        }
    }
}
