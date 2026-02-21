// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace GitReleaseNoteGenerator.Infrastructure;

/// <summary>
/// Reads GitHub Actions environment variables.
/// </summary>
public static class GitHubActionEnvironment
{
    /// <summary>
    /// Gets the GITHUB_TOKEN environment variable.
    /// </summary>
    public static string? Token => Environment.GetEnvironmentVariable("GITHUB_TOKEN");

    /// <summary>
    /// Gets the repository owner from GITHUB_REPOSITORY (format: "owner/repo").
    /// </summary>
    public static string? RepositoryOwner
    {
        get
        {
            var repo = Environment.GetEnvironmentVariable("GITHUB_REPOSITORY");
            if (string.IsNullOrEmpty(repo))
            {
                return null;
            }

            var parts = repo.Split('/');
            return parts.Length >= 2 ? parts[0] : null;
        }
    }

    /// <summary>
    /// Gets the repository name from GITHUB_REPOSITORY (format: "owner/repo").
    /// </summary>
    public static string? RepositoryName
    {
        get
        {
            var repo = Environment.GetEnvironmentVariable("GITHUB_REPOSITORY");
            if (string.IsNullOrEmpty(repo))
            {
                return null;
            }

            var parts = repo.Split('/');
            return parts.Length >= 2 ? parts[1] : null;
        }
    }

    /// <summary>
    /// Gets the GITHUB_REF environment variable.
    /// </summary>
    public static string? Ref => Environment.GetEnvironmentVariable("GITHUB_REF");

    /// <summary>
    /// Gets the GITHUB_SHA environment variable.
    /// </summary>
    public static string? Sha => Environment.GetEnvironmentVariable("GITHUB_SHA");

    /// <summary>
    /// Gets the GITHUB_OUTPUT file path environment variable.
    /// </summary>
    public static string? OutputFile => Environment.GetEnvironmentVariable("GITHUB_OUTPUT");
}
