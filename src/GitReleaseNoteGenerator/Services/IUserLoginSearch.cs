// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace GitReleaseNoteGenerator.Services;

/// <summary>
/// Looks up the GitHub login associated with an email address. This is a seam over the
/// GitHub "search users by email" API so that <see cref="AuthorResolver"/> can be unit tested
/// without performing real network calls.
/// </summary>
public interface IUserLoginSearch
{
    /// <summary>
    /// Finds the GitHub login for the given email address.
    /// </summary>
    /// <param name="email">The email address to resolve.</param>
    /// <returns>The matching login, or null if no user was found.</returns>
    Task<string?> FindLoginByEmailAsync(string email);
}
