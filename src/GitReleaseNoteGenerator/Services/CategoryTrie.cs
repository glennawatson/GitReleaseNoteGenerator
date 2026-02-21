// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections;

namespace GitReleaseNoteGenerator.Services;

/// <summary>
/// A trie (prefix tree) that maps commit message prefixes to categories.
/// Supports efficient longest-prefix-match lookup for categorizing commits.
/// </summary>
public sealed class CategoryTrie : IEnumerable<(int Priority, string Category, string[] Prefixes)>
{
    /// <summary>
    /// The root node of the trie structure.
    /// </summary>
    private readonly TrieNode _root = new();

    /// <summary>
    /// All registered category groups with their priorities and prefix arrays.
    /// </summary>
    private readonly List<(int Priority, string Category, string[] Prefixes)> _groups = [];

    /// <summary>
    /// The fallback category name returned when no prefix matches a message.
    /// </summary>
    private readonly string _otherValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="CategoryTrie"/> class.
    /// </summary>
    /// <param name="otherCategoryName">The fallback category name for unmatched messages.</param>
    /// <param name="categories">The categories with their priorities and prefix mappings.</param>
    public CategoryTrie(string otherCategoryName, IEnumerable<(int Priority, string Category, string[] Prefixes)> categories)
    {
        ArgumentNullException.ThrowIfNull(categories);

        _otherValue = otherCategoryName;
        foreach (var (priority, category, prefixes) in categories)
        {
            Add(priority, category, prefixes);
        }
    }

    /// <summary>
    /// Gets the fallback category for messages that don't match any prefix.
    /// </summary>
    public (int Priority, string Category) OtherCategory => (int.MaxValue, _otherValue);

    /// <summary>
    /// Gets the number of category groups in the trie.
    /// </summary>
    public int Count => _groups.Count;

    /// <summary>
    /// Indexer to look up the category for a given message.
    /// </summary>
    /// <param name="message">The commit message to categorize.</param>
    /// <returns>The priority and category name.</returns>
    public (int Priority, string Name) this[string message] => Lookup(message);

    /// <summary>
    /// Looks up the category for a given message by matching its prefix.
    /// </summary>
    /// <param name="message">The commit message to categorize.</param>
    /// <returns>The priority and category name, or <see cref="OtherCategory"/> if no prefix matches.</returns>
    public (int Priority, string Name) Lookup(string message)
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

    /// <inheritdoc/>
    public IEnumerator<(int Priority, string Category, string[] Prefixes)> GetEnumerator() => _groups.GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Registers a category group and inserts all its prefixes into the trie.
    /// </summary>
    /// <param name="priority">The sort priority for the category.</param>
    /// <param name="category">The category name.</param>
    /// <param name="prefixes">The commit message prefixes that map to this category.</param>
    private void Add(int priority, string category, string[] prefixes)
    {
        _groups.Add((priority, category, prefixes));
        foreach (var prefix in prefixes)
        {
            AddToTrie(priority, prefix, category);
        }
    }

    /// <summary>
    /// Inserts a single prefix into the trie, creating nodes as needed.
    /// </summary>
    /// <param name="priority">The sort priority for the category.</param>
    /// <param name="prefix">The commit message prefix to insert.</param>
    /// <param name="category">The category name to associate with the prefix.</param>
    private void AddToTrie(int priority, string prefix, string category)
    {
        var node = _root;
        foreach (var character in prefix)
        {
            var ch = char.ToLowerInvariant(character);
            if (!node.Children.TryGetValue(ch, out var childNode))
            {
                childNode = new TrieNode();
                node.Children[ch] = childNode;
            }

            node = childNode;
        }

        node.Priority = priority;
        node.Category = category;
    }

    /// <summary>
    /// A single node in the prefix trie. Leaf nodes store the matched category and priority.
    /// </summary>
    private sealed class TrieNode
    {
        /// <summary>
        /// Gets the child nodes keyed by lowercase character.
        /// </summary>
        public Dictionary<char, TrieNode> Children { get; } = [];

        /// <summary>
        /// Gets or sets the category name if this node terminates a prefix, or null otherwise.
        /// </summary>
        public string? Category { get; set; }

        /// <summary>
        /// Gets or sets the sort priority for this node's category.
        /// </summary>
        public int Priority { get; set; } = int.MaxValue;
    }
}
