// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using GitReleaseNoteGenerator.Services;

namespace GitReleaseNoteGenerator.Tests.Services;

/// <summary>
/// Tests for <see cref="CategoryTrie"/>.
/// </summary>
public class CategoryTrieTests
{
    /// <summary>
    /// Prefixes that map to the Features category.
    /// </summary>
    private static readonly string[] FeatPrefixes = ["feat"];

    /// <summary>
    /// Prefixes that map to the Fixes category.
    /// </summary>
    private static readonly string[] FixPrefixes = ["fix", "bug"];

    /// <summary>
    /// Prefixes that map to the Documentation category.
    /// </summary>
    private static readonly string[] DocPrefixes = ["doc"];

    /// <summary>
    /// Tests that a message matching a prefix returns the correct category.
    /// </summary>
    [Test]
    public async Task Lookup_WithMatchingPrefix_ReturnsCorrectCategory()
    {
        var trie = CreateDefaultTrie();

        var result = trie.Lookup("feat: add new button");

        await Assert.That(result.Name).IsEqualTo("Features");
        await Assert.That(result.Priority).IsEqualTo(1);
    }

    /// <summary>
    /// Tests that prefix matching is case insensitive.
    /// </summary>
    [Test]
    public async Task Lookup_IsCaseInsensitive()
    {
        var trie = CreateDefaultTrie();

        var result = trie.Lookup("FEAT: add new button");

        await Assert.That(result.Name).IsEqualTo("Features");
    }

    /// <summary>
    /// Tests that an unmatched message returns the Other category.
    /// </summary>
    [Test]
    public async Task Lookup_WithNoMatch_ReturnsOtherCategory()
    {
        var trie = CreateDefaultTrie();

        var result = trie.Lookup("random commit message");

        await Assert.That(result.Name).IsEqualTo("Other");
        await Assert.That(result.Priority).IsEqualTo(int.MaxValue);
    }

    /// <summary>
    /// Tests that the fix prefix is correctly matched.
    /// </summary>
    [Test]
    public async Task Lookup_WithFixPrefix_ReturnsFixes()
    {
        var trie = CreateDefaultTrie();

        var result = trie.Lookup("fix: resolve null reference");

        await Assert.That(result.Name).IsEqualTo("Fixes");
    }

    /// <summary>
    /// Tests that the bug prefix also maps to Fixes.
    /// </summary>
    [Test]
    public async Task Lookup_WithBugPrefix_ReturnsFixes()
    {
        var trie = CreateDefaultTrie();

        var result = trie.Lookup("bug: handle edge case");

        await Assert.That(result.Name).IsEqualTo("Fixes");
    }

    /// <summary>
    /// Tests that the indexer works the same as Lookup.
    /// </summary>
    [Test]
    public async Task Indexer_ReturnsSameAsLookup()
    {
        var trie = CreateDefaultTrie();

        var indexerResult = trie["feat: something"];
        var lookupResult = trie.Lookup("feat: something");

        await Assert.That(indexerResult).IsEqualTo(lookupResult);
    }

    /// <summary>
    /// Tests that Count reflects the number of category groups.
    /// </summary>
    [Test]
    public async Task Count_ReturnsNumberOfGroups()
    {
        var trie = CreateDefaultTrie();

        await Assert.That(trie.Count).IsEqualTo(3);
    }

    /// <summary>
    /// Tests that an empty message returns Other.
    /// </summary>
    [Test]
    public async Task Lookup_WithEmptyMessage_ReturnsOther()
    {
        var trie = CreateDefaultTrie();

        var result = trie.Lookup(string.Empty);

        await Assert.That(result.Name).IsEqualTo("Other");
    }

    /// <summary>
    /// Tests enumeration returns all groups.
    /// </summary>
    [Test]
    public async Task Enumeration_ReturnsAllGroups()
    {
        var trie = CreateDefaultTrie();

        var groups = trie.ToList();

        await Assert.That(groups).Count().IsEqualTo(3);
        await Assert.That(groups[0].Category).IsEqualTo("Features");
        await Assert.That(groups[1].Category).IsEqualTo("Fixes");
        await Assert.That(groups[2].Category).IsEqualTo("Documentation");
    }

    /// <summary>
    /// Creates a trie with Features, Fixes, and Documentation categories for testing.
    /// </summary>
    /// <returns>A configured <see cref="CategoryTrie"/> instance.</returns>
    private static CategoryTrie CreateDefaultTrie() => new(
        "Other",
        [
            (1, "Features", FeatPrefixes),
            (2, "Fixes", FixPrefixes),
            (3, "Documentation", DocPrefixes),
        ]);
}
