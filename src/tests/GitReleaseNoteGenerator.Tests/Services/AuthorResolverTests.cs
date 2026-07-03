// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net;

using GitReleaseNoteGenerator.Models;
using GitReleaseNoteGenerator.Services;

using Microsoft.Extensions.Logging.Abstractions;

using Refit;

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
    /// The normalized display name that <see cref="GlennName"/> collapses to when no login is found.
    /// </summary>
    private const string GlennNormalizedName = "GlennWatson";

    /// <summary>
    /// A GitHub login used as a primary author in the multi-contributor tests.
    /// </summary>
    private const string OctocatLogin = "octocat";

    /// <summary>
    /// Tests that the GitHub-client constructor wires up the default API-backed login search.
    /// </summary>
    [Test]
    public async Task Constructor_WithGitHubClient_CreatesResolver()
    {
        var resolver = new AuthorResolver(GitHubClientFactory.Create("ghp_example"), NullLogger.Instance);

        await Assert.That(resolver).IsNotNull();
    }

    /// <summary>
    /// Tests that an already-resolved login is returned without querying the search API.
    /// </summary>
    [Test]
    public async Task ResolveAsync_WithExistingLogin_DoesNotQuerySearch()
    {
        var search = new FakeUserLoginSearch();
        var resolver = new AuthorResolver(search, NullLogger.Instance);

        var result = await resolver.ResolveAsync(new(OctocatLogin, "Octo Cat", "octo@example.com"));

        await Assert.That(result).IsEqualTo(OctocatLogin);
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
    /// Tests that a contributor with no email falls back to the name without querying the API.
    /// </summary>
    [Test]
    public async Task ResolveAsync_WithNoEmail_FallsBackToNameWithoutQuery()
    {
        var search = new FakeUserLoginSearch();
        var resolver = new AuthorResolver(search, NullLogger.Instance);

        var result = await resolver.ResolveAsync(new(null, GlennName, null));

        await Assert.That(result).IsEqualTo(GlennNormalizedName);
        await Assert.That(search.CallCount).IsEqualTo(0);
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

        await Assert.That(result).IsEqualTo(GlennNormalizedName);
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

        await Assert.That(result).IsEqualTo(GlennNormalizedName);
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
    /// Tests that resolving with search disabled does not query the search API for an email that
    /// has not already been cached, falling back to the normalized display name instead. This is
    /// the behavior the full-history author walk relies on to avoid the search rate limit.
    /// </summary>
    [Test]
    public async Task GetResolvedAuthorsAsync_WhenSearchDisabled_DoesNotQueryForUnseenEmail()
    {
        var search = new FakeUserLoginSearch(new() { [GlennEmail] = GlennLogin });
        var resolver = new AuthorResolver(search, NullLogger.Instance);
        var commit = CreateCommit(
            $"feat: add feature\n\nCo-authored-by: {GlennName} <{GlennEmail}>",
            authorLogin: OctocatLogin);

        var authors = await resolver.GetResolvedAuthorsAsync(commit, allowSearch: false);

        await Assert.That(search.CallCount).IsEqualTo(0);
        await Assert.That(authors).Contains(OctocatLogin);
        await Assert.That(authors).Contains(GlennNormalizedName);
    }

    /// <summary>
    /// Tests that resolving with search disabled still returns a login for an email cached by an
    /// earlier search-enabled pass, so the small "since last release" set primes the cache and the
    /// history walk reuses it without new queries.
    /// </summary>
    [Test]
    public async Task GetResolvedAuthorsAsync_WhenSearchDisabledButCached_UsesCachedLogin()
    {
        var search = new FakeUserLoginSearch(new() { [GlennEmail] = GlennLogin });
        var resolver = new AuthorResolver(search, NullLogger.Instance);
        var commit = CreateCommit(
            $"feat: add feature\n\nCo-authored-by: {GlennName} <{GlennEmail}>",
            authorLogin: OctocatLogin);

        await resolver.GetResolvedAuthorsAsync(commit, allowSearch: true);
        var authors = await resolver.GetResolvedAuthorsAsync(commit, allowSearch: false);

        await Assert.That(search.CallCount).IsEqualTo(1);
        await Assert.That(authors).Contains(GlennLogin);
        await Assert.That(authors).Contains(OctocatLogin);
    }

    /// <summary>
    /// Creates a test <see cref="GitHubCommit"/> with the specified message and optional author login.
    /// </summary>
    /// <param name="message">The commit message.</param>
    /// <param name="authorLogin">The GitHub login of the author, or null.</param>
    /// <returns>A configured <see cref="GitHubCommit"/> for testing.</returns>
    private static GitHubCommit CreateCommit(string message, string? authorLogin = null) =>
        new(
            "abc123",
            new GitCommitDetail(message, Author: null, Committer: null),
            authorLogin is not null ? new GitHubUser(authorLogin) : null,
            Committer: null);

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
        public async Task<string?> FindLoginByEmailAsync(string email)
        {
            CallCount++;
            if (_throwApiException)
            {
                throw await CreateApiExceptionAsync().ConfigureAwait(false);
            }

            _responses.TryGetValue(email, out var login);
            return login;
        }

        /// <summary>
        /// Builds a Refit <see cref="ApiException"/> equivalent to a failed search-users call.
        /// </summary>
        /// <returns>The constructed exception.</returns>
        private static async Task<ApiException> CreateApiExceptionAsync()
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/search/users");
            using var response = new HttpResponseMessage(HttpStatusCode.UnprocessableEntity) { RequestMessage = request };
            return await ApiException.Create(request, HttpMethod.Get, response, new RefitSettings()).ConfigureAwait(false);
        }
    }
}
