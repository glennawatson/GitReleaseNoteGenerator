// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace GitReleaseNoteGenerator.Models;

/// <summary>
/// A GitHub account as attributed by the API to a commit author, committer, or search result.
/// </summary>
/// <param name="Login">The GitHub login, or null when the API could not attribute an account.</param>
public sealed record GitHubUser(string? Login);
