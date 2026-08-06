// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace GitReleaseNoteGenerator.Models;

/// <summary>
/// A single category group registered in the <see cref="Services.CategoryTrie"/>: a category
/// name and the commit message prefixes that map to it. Sort priority is not carried here — the
/// trie derives it from registration order, so the declaration order of the groups is the
/// single source of truth for how sections are ranked.
/// </summary>
/// <param name="Category">The category display name.</param>
/// <param name="Prefixes">The commit message prefixes that map to this category.</param>
internal sealed record CategoryGroup(string Category, IReadOnlyList<string> Prefixes);
