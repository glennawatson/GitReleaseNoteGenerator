// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace GitReleaseNoteGenerator.Models;

/// <summary>
/// Defines a commit category with its display properties and matching prefixes.
/// </summary>
/// <param name="Name">The display name of the category (e.g., "Features", "Fixes").</param>
/// <param name="Emoji">The emoji displayed in the release notes heading.</param>
/// <param name="Priority">Sort order priority (lower = higher priority).</param>
/// <param name="Prefixes">Commit message prefixes that map to this category.</param>
public sealed record CategoryDefinition(string Name, string Emoji, int Priority, IReadOnlyList<string> Prefixes);
