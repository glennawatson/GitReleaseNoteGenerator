// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

using GitReleaseNoteGenerator.Models;

namespace GitReleaseNoteGenerator.Infrastructure;

/// <summary>
/// System.Text.Json source-generation context for the GitHub API payloads. Using a compile-time
/// context (rather than reflection-based serialization) keeps deserialization trim- and
/// AOT-compatible. GitHub returns snake_case field names, mapped from the PascalCase DTO
/// properties by the <see cref="JsonKnownNamingPolicy.SnakeCaseLower"/> policy.
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(GitHubRepository))]
[JsonSerializable(typeof(GitHubRelease))]
[JsonSerializable(typeof(GitHubComparison))]
[JsonSerializable(typeof(GitHubCommit))]
[JsonSerializable(typeof(IReadOnlyList<GitHubCommit>))]
[JsonSerializable(typeof(GitHubUserSearchResult))]
internal sealed partial class GitHubJsonContext : JsonSerializerContext;
