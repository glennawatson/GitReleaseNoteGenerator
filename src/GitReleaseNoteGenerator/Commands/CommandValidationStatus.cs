// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace GitReleaseNoteGenerator.Commands;

/// <summary>
/// The outcome of validating the required command values.
/// </summary>
internal enum CommandValidationStatus
{
    /// <summary>
    /// All required values are present.
    /// </summary>
    Valid,

    /// <summary>
    /// The GitHub token is missing.
    /// </summary>
    TokenMissing,

    /// <summary>
    /// The repository owner and/or name are missing.
    /// </summary>
    RepositoryMissing,

    /// <summary>
    /// The release version is missing.
    /// </summary>
    VersionMissing,
}
