// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

using GitReleaseNoteGenerator.Models;

namespace GitReleaseNoteGenerator.Services;

/// <summary>
/// A trie (prefix tree) that maps commit message prefixes to categories.
/// Supports efficient longest-prefix-match lookup for categorizing commits.
/// </summary>
internal sealed class CategoryTrie
{
    /// <summary>The root node of the trie structure.</summary>
    private readonly TrieNode _root = new();

    /// <summary>All registered category groups with their priorities and prefix arrays.</summary>
    private readonly List<CategoryGroup> _groups = [];

    /// <summary>The fallback category name returned when no prefix matches a message.</summary>
    private readonly string _otherValue;

    /// <summary>Initializes a new instance of the <see cref="CategoryTrie"/> class.</summary>
    /// <param name="otherCategoryName">The fallback category name for unmatched messages.</param>
    /// <param name="categories">
    /// The categories and their prefix mappings, most important first. Sort priority is assigned
    /// from this order, so a category listed earlier outranks one listed later.
    /// </param>
    internal CategoryTrie(string otherCategoryName, IEnumerable<CategoryGroup> categories)
    {
        ArgumentNullException.ThrowIfNull(categories);

        _otherValue = otherCategoryName;
        foreach (var group in categories)
        {
            Add(group);
        }
    }

    /// <summary>Gets the fallback category for messages that don't match any prefix.</summary>
    internal (int Priority, string Category) OtherCategory => (int.MaxValue, _otherValue);

    /// <summary>Gets the number of category groups in the trie.</summary>
    internal int Count => _groups.Count;

    /// <summary>
    /// Gets the registered category groups in registration order, which is also priority order.
    /// This is the order release note sections are emitted in.
    /// </summary>
    internal IReadOnlyList<CategoryGroup> Groups => _groups;

    /// <summary>Indexer to look up the category for a given message.</summary>
    /// <param name="message">The commit message to categorize.</param>
    /// <returns>The priority and category name.</returns>
    internal (int Priority, string Name) this[string message] => Lookup(message);

    /// <summary>Looks up the category for a given message by matching its prefix.</summary>
    /// <param name="message">The commit message to categorize.</param>
    /// <returns>The priority and category name, or <see cref="OtherCategory"/> if no prefix matches.</returns>
    internal (int Priority, string Name) Lookup(string message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var node = _root;
        foreach (var ch in message)
        {
            if (!node.Children.TryGetValue(char.ToLowerInvariant(ch), out node))
            {
                return OtherCategory;
            }

            if (node.Category is not null)
            {
                return (node.Priority, node.Category);
            }
        }

        return OtherCategory;
    }

    /// <summary>
    /// Registers a category group and inserts all its prefixes into the trie. The group's sort
    /// priority is its 1-based registration order, so the first group registered outranks the rest.
    /// </summary>
    /// <param name="group">The category group to register.</param>
    private void Add(CategoryGroup group)
    {
        _groups.Add(group);
        var priority = _groups.Count;
        foreach (var prefix in group.Prefixes)
        {
            AddToTrie(priority, prefix, group.Category);
        }
    }

    /// <summary>Inserts a single prefix into the trie, creating nodes as needed.</summary>
    /// <param name="priority">The sort priority for the category.</param>
    /// <param name="prefix">The commit message prefix to insert.</param>
    /// <param name="category">The category name to associate with the prefix.</param>
    private void AddToTrie(int priority, string prefix, string category)
    {
        var node = _root;
        foreach (var character in prefix)
        {
            var ch = char.ToLowerInvariant(character);
            ref var childNode = ref CollectionsMarshal.GetValueRefOrAddDefault(node.Children, ch, out _);
            childNode ??= new();

            node = childNode;
        }

        node.Priority = priority;
        node.Category = category;
    }

    /// <summary>A single node in the prefix trie. Leaf nodes store the matched category and priority.</summary>
    private sealed class TrieNode
    {
        /// <summary>Gets the child nodes keyed by lowercase character.</summary>
        public Dictionary<char, TrieNode> Children { get; } = [];

        /// <summary>Gets or sets the category name if this node terminates a prefix, or null otherwise.</summary>
        public string? Category { get; set; }

        /// <summary>Gets or sets the sort priority for this node's category.</summary>
        public int Priority { get; set; } = int.MaxValue;
    }
}
