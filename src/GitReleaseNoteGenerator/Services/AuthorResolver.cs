// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using GitReleaseNoteGenerator.Models;

using Microsoft.Extensions.Logging;

using Octokit;

namespace GitReleaseNoteGenerator.Services;

/// <summary>
/// Resolves commit contributors to canonical GitHub logins so that the same person is not
/// listed multiple times (for example, once by their GitHub login and again by the display
/// name on a "Co-authored-by:" trailer). Resolution proceeds from cheapest to most expensive:
/// an already-resolved login, a login embedded in a GitHub noreply email, then a cached
/// GitHub "search users by email" API call. The normalized display name is used as a fallback.
/// </summary>
public sealed partial class AuthorResolver
{
    /// <summary>
    /// The seam used to look up GitHub logins from email addresses.
    /// </summary>
    private readonly IUserLoginSearch _userSearch;

    /// <summary>
    /// The logger for diagnostic messages.
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// Caches the result of resolving an email to a login. A null value records a previously
    /// attempted lookup that produced no match, so the API is not queried for it again.
    /// </summary>
    private readonly Dictionary<string, string?> _emailToLoginCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthorResolver"/> class backed by the
    /// GitHub API.
    /// </summary>
    /// <param name="client">An authenticated GitHub client.</param>
    /// <param name="logger">Logger for diagnostic messages.</param>
    public AuthorResolver(GitHubClient client, ILogger logger)
        : this(new GitHubUserLoginSearch(client, logger), logger)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthorResolver"/> class with an explicit
    /// login-search seam. Intended for testing.
    /// </summary>
    /// <param name="userSearch">The login-search implementation.</param>
    /// <param name="logger">Logger for diagnostic messages.</param>
    internal AuthorResolver(IUserLoginSearch userSearch, ILogger logger)
    {
        _userSearch = userSearch;
        _logger = logger;
    }

    /// <summary>
    /// Resolves every contributor of a commit to a canonical identifier and returns the
    /// distinct, case-insensitively de-duplicated set.
    /// </summary>
    /// <param name="commit">The commit whose contributors should be resolved.</param>
    /// <returns>A sorted set of resolved author identifiers.</returns>
    public async Task<SortedSet<string>> GetResolvedAuthorsAsync(GitHubCommit commit)
    {
        ArgumentNullException.ThrowIfNull(commit);

        var authors = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var contributor in AuthorExtractor.GetContributors(commit))
        {
            authors.Add(await ResolveAsync(contributor).ConfigureAwait(false));
        }

        return authors;
    }

    /// <summary>
    /// Resolves a single contributor to a canonical identifier, consulting the GitHub API
    /// only when the identity cannot be determined locally.
    /// </summary>
    /// <param name="contributor">The contributor candidate to resolve.</param>
    /// <returns>The resolved identifier.</returns>
    public async Task<string> ResolveAsync(CommitContributor contributor)
    {
        ArgumentNullException.ThrowIfNull(contributor);

        if (!string.IsNullOrWhiteSpace(contributor.Login))
        {
            return contributor.Login;
        }

        // The '??' short-circuits, so the API is queried only when no noreply login was embedded.
        var login = AuthorExtractor.TryGetLoginFromNoReplyEmail(contributor.Email)
            ?? await ResolveLoginByEmailAsync(contributor.Email).ConfigureAwait(false);

        return login ?? AuthorExtractor.NormalizeAuthorName(contributor.Name ?? string.Empty);
    }

    /// <summary>
    /// Looks up a GitHub login for a real (non-noreply) email via the search-users API,
    /// caching both successful and unsuccessful results.
    /// </summary>
    /// <param name="email">The email address to resolve, or null.</param>
    /// <returns>The matching login, or null if none could be found.</returns>
    private async Task<string?> ResolveLoginByEmailAsync(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var key = email.Trim();
        if (_emailToLoginCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        string? login = null;
        try
        {
            login = await _userSearch.FindLoginByEmailAsync(key).ConfigureAwait(false);
        }
        catch (ApiException ex)
        {
            LogEmailLookupFailed(email, ex);
        }

        _emailToLoginCache[key] = login;
        return login;
    }

    /// <summary>
    /// Logs that a GitHub email-to-login lookup failed and fell back to the display name.
    /// </summary>
    /// <param name="email">The email that could not be resolved.</param>
    /// <param name="exception">The exception that caused the failure.</param>
    [LoggerMessage(Level = LogLevel.Debug, Message = "Could not resolve a GitHub login for {Email}; falling back to display name")]
    private partial void LogEmailLookupFailed(string email, Exception exception);
}
