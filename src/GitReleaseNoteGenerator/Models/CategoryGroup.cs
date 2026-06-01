// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace GitReleaseNoteGenerator.Models;

/// <summary>
/// A single category group registered in the <see cref="Services.CategoryTrie"/>: a category
/// name, its sort priority, and the commit message prefixes that map to it.
/// </summary>
/// <param name="Priority">The sort priority for the category (lower = higher priority).</param>
/// <param name="Category">The category display name.</param>
/// <param name="Prefixes">The commit message prefixes that map to this category.</param>
internal sealed record CategoryGroup(int Priority, string Category, IReadOnlyList<string> Prefixes);
