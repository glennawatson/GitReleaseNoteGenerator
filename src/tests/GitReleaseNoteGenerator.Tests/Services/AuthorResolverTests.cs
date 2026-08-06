// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net;

using GitReleaseNoteGenerator.Models;
using GitReleaseNoteGenerator.Services;

using Microsoft.Extensions.Logging.Abstractions;

using Refit;

namespace GitReleaseNoteGenerator.Tests.Services;

/// <summary>Tests for <see cref="AuthorResolver"/>, exercising the email-to-login API tier through a hand-rolled <see cref="IUserLoginSearch"/> test double (no mocking framework).</summary>
public class AuthorResolverTests
{
    /// <summary>A contributor display name used across the resolution tests.</summary>
    private const string ContributorName = "Test User";

    /// <summary>The GitHub login that the display name and email resolve to.</summary>
    private const string ContributorLogin = "testuser";

    /// <summary>A real (non-noreply) email used across the resolution tests.</summary>
    private const string ContributorEmail = "testuser@example.com";

    /// <summary>A GitHub login used as a primary author in the multi-contributor tests.</summary>
    private const string OctocatLogin = "octocat";

    /// <summary>The stand-in access token used when only resolver construction is under test.</summary>
    private const string ExampleToken = "ghp_example";

    /// <summary>Tests that the GitHub-client constructor wires up the default API-backed login search.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ConstructorWithGitHubClientCreatesResolver()
    {
        var resolver = new AuthorResolver(GitHubClientFactory.Create(ExampleToken), NullLogger.Instance);

        await Assert.That(resolver).IsNotNull();
    }

    /// <summary>Tests that an already-resolved login is returned without querying the search API.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ResolveAsyncWithExistingLoginDoesNotQuerySearch()
    {
        var search = new FakeUserLoginSearch();
        var resolver = new AuthorResolver(search, NullLogger.Instance);

        var result = await resolver.ResolveAsync(new(OctocatLogin, "Octo Cat", "octo@example.com"));

        await Assert.That(result.Value).IsEqualTo(OctocatLogin);
        await Assert.That(result.IsLogin).IsTrue();
        await Assert.That(search.CallCount).IsEqualTo(0);
    }

    /// <summary>Tests that a GitHub noreply email is resolved locally without querying the search API.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ResolveAsyncWithNoReplyEmailDoesNotQuerySearch()
    {
        var search = new FakeUserLoginSearch();
        var resolver = new AuthorResolver(search, NullLogger.Instance);

        var result = await resolver.ResolveAsync(
            new(null, ContributorName, $"12345+{ContributorLogin}@users.noreply.github.com"));

        await Assert.That(result.Value).IsEqualTo(ContributorLogin);
        await Assert.That(result.IsLogin).IsTrue();
        await Assert.That(search.CallCount).IsEqualTo(0);
    }

    /// <summary>Tests that a real email is resolved to a login via the search API.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ResolveAsyncWithRealEmailReturnsLoginFromSearch()
    {
        var search = new FakeUserLoginSearch(new() { [ContributorEmail] = ContributorLogin });
        var resolver = new AuthorResolver(search, NullLogger.Instance);

        var result = await resolver.ResolveAsync(new(null, ContributorName, ContributorEmail));

        await Assert.That(result.Value).IsEqualTo(ContributorLogin);
        await Assert.That(result.IsLogin).IsTrue();
        await Assert.That(search.CallCount).IsEqualTo(1);
    }

    /// <summary>Tests that a contributor with no email falls back to the name without querying the API.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ResolveAsyncWithNoEmailFallsBackToNameWithoutQuery()
    {
        var search = new FakeUserLoginSearch();
        var resolver = new AuthorResolver(search, NullLogger.Instance);

        var result = await resolver.ResolveAsync(new(null, ContributorName, null));

        await Assert.That(result.Value).IsEqualTo(ContributorName);
        await Assert.That(result.IsLogin).IsFalse();
        await Assert.That(search.CallCount).IsEqualTo(0);
    }

    /// <summary>Tests that an unresolvable email falls back to the normalized display name.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ResolveAsyncWithUnresolvableEmailFallsBackToName()
    {
        var search = new FakeUserLoginSearch();
        var resolver = new AuthorResolver(search, NullLogger.Instance);

        var result = await resolver.ResolveAsync(new(null, ContributorName, ContributorEmail));

        await Assert.That(result.Value).IsEqualTo(ContributorName);
        await Assert.That(result.IsLogin).IsFalse();
    }

    /// <summary>Tests that a successful lookup is cached so the search API is queried only once.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ResolveAsyncCachesSuccessfulLookup()
    {
        var search = new FakeUserLoginSearch(new() { [ContributorEmail] = ContributorLogin });
        var resolver = new AuthorResolver(search, NullLogger.Instance);
        var contributor = new CommitContributor(null, ContributorName, ContributorEmail);

        await resolver.ResolveAsync(contributor);
        await resolver.ResolveAsync(contributor);

        await Assert.That(search.CallCount).IsEqualTo(1);
    }

    /// <summary>Tests that an unsuccessful lookup is cached so the search API is not queried again.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ResolveAsyncCachesNegativeLookup()
    {
        var search = new FakeUserLoginSearch();
        var resolver = new AuthorResolver(search, NullLogger.Instance);
        var contributor = new CommitContributor(null, ContributorName, ContributorEmail);

        await resolver.ResolveAsync(contributor);
        await resolver.ResolveAsync(contributor);

        await Assert.That(search.CallCount).IsEqualTo(1);
    }

    /// <summary>Tests that an API failure is swallowed and resolution falls back to the display name.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task ResolveAsyncWhenSearchThrowsFallsBackToName()
    {
        var search = new FakeUserLoginSearch(throwApiException: true);
        var resolver = new AuthorResolver(search, NullLogger.Instance);

        var result = await resolver.ResolveAsync(new(null, ContributorName, ContributorEmail));

        await Assert.That(result.Value).IsEqualTo(ContributorName);
        await Assert.That(result.IsLogin).IsFalse();
    }

    /// <summary>Tests that a primary author and a co-author whose email resolves to the same login collapse into a single contributor.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task GetResolvedAuthorsAsyncWithApiResolvedCoAuthorCollapsesToSingleLogin()
    {
        var search = new FakeUserLoginSearch(new() { [ContributorEmail] = ContributorLogin });
        var resolver = new AuthorResolver(search, NullLogger.Instance);
        var commit = CreateCommit(
            $"feat: add feature\n\nCo-authored-by: {ContributorName} <{ContributorEmail}>",
            authorLogin: ContributorLogin);

        var authors = await resolver.GetResolvedAuthorsAsync(commit);

        await Assert.That(authors.Count).IsEqualTo(1);
        await Assert.That(Values(authors)).Contains(ContributorLogin);
    }

    /// <summary>
    /// Tests that resolving with search disabled does not query the search API for an email that
    /// has not already been cached, falling back to the normalized display name instead. This is
    /// the behavior the full-history author walk relies on to avoid the search rate limit.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task GetResolvedAuthorsAsyncWhenSearchDisabledDoesNotQueryForUnseenEmail()
    {
        var search = new FakeUserLoginSearch(new() { [ContributorEmail] = ContributorLogin });
        var resolver = new AuthorResolver(search, NullLogger.Instance);
        var commit = CreateCommit(
            $"feat: add feature\n\nCo-authored-by: {ContributorName} <{ContributorEmail}>",
            authorLogin: OctocatLogin);

        var authors = await resolver.GetResolvedAuthorsAsync(commit, allowSearch: false);

        await Assert.That(search.CallCount).IsEqualTo(0);
        await Assert.That(Values(authors)).Contains(OctocatLogin);
        await Assert.That(Values(authors)).Contains(ContributorName);
    }

    /// <summary>
    /// Tests that resolving with search disabled still returns a login for an email cached by an
    /// earlier search-enabled pass, so the small "since last release" set primes the cache and the
    /// history walk reuses it without new queries.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task GetResolvedAuthorsAsyncWhenSearchDisabledButCachedUsesCachedLogin()
    {
        var search = new FakeUserLoginSearch(new() { [ContributorEmail] = ContributorLogin });
        var resolver = new AuthorResolver(search, NullLogger.Instance);
        var commit = CreateCommit(
            $"feat: add feature\n\nCo-authored-by: {ContributorName} <{ContributorEmail}>",
            authorLogin: OctocatLogin);

        await resolver.GetResolvedAuthorsAsync(commit, allowSearch: true);
        var authors = await resolver.GetResolvedAuthorsAsync(commit, allowSearch: false);

        await Assert.That(search.CallCount).IsEqualTo(1);
        await Assert.That(Values(authors)).Contains(ContributorLogin);
        await Assert.That(Values(authors)).Contains(OctocatLogin);
    }

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

    /// <summary>A hand-rolled <see cref="IUserLoginSearch"/> test double that returns canned results and records how many times it was queried.</summary>
    private sealed class FakeUserLoginSearch : IUserLoginSearch
    {
        /// <summary>The configured email-to-login responses.</summary>
        private readonly Dictionary<string, string?> _responses;

        /// <summary>Whether each lookup should throw an <see cref="ApiException"/>.</summary>
        private readonly bool _throwApiException;

        /// <summary>Initializes a new instance of the <see cref="FakeUserLoginSearch"/> class.</summary>
        /// <param name="responses">The configured email-to-login responses, or null for none.</param>
        /// <param name="throwApiException">Whether each lookup should throw an API exception.</param>
        public FakeUserLoginSearch(Dictionary<string, string?>? responses = null, bool throwApiException = false)
        {
            _responses = responses ?? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            _throwApiException = throwApiException;
        }

        /// <summary>Gets the number of times the search was queried.</summary>
        public int CallCount { get; private set; }

        /// <inheritdoc/>
        public async Task<string?> FindLoginByEmailAsync(string email)
        {
            CallCount++;
            if (_throwApiException)
            {
                throw await CreateApiExceptionAsync().ConfigureAwait(false);
            }

            _ = _responses.TryGetValue(email, out var login);
            return login;
        }

        /// <summary>Builds a Refit <see cref="ApiException"/> equivalent to a failed search-users call.</summary>
        /// <returns>The constructed exception.</returns>
        private static async Task<ApiException> CreateApiExceptionAsync()
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/search/users");
            using var response = new HttpResponseMessage(HttpStatusCode.UnprocessableEntity) { RequestMessage = request };
            return await ApiException.Create(request, HttpMethod.Get, response, new()).ConfigureAwait(false);
        }
    }
}
