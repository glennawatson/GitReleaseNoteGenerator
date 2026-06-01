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
    /// The Features category name.
    /// </summary>
    private const string FeaturesCategory = "Features";

    /// <summary>
    /// The Fixes category name.
    /// </summary>
    private const string FixesCategory = "Fixes";

    /// <summary>
    /// The Documentation category name.
    /// </summary>
    private const string DocumentationCategory = "Documentation";

    /// <summary>
    /// Priority assigned to the Features category in the test trie.
    /// </summary>
    private const int FeaturesPriority = 1;

    /// <summary>
    /// Priority assigned to the Fixes category in the test trie.
    /// </summary>
    private const int FixesPriority = 2;

    /// <summary>
    /// Priority assigned to the Documentation category in the test trie.
    /// </summary>
    private const int DocumentationPriority = 3;

    /// <summary>
    /// The number of category groups configured in the test trie.
    /// </summary>
    private const int ExpectedGroupCount = 3;

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

        await Assert.That(result.Name).IsEqualTo(FeaturesCategory);
        await Assert.That(result.Priority).IsEqualTo(FeaturesPriority);
    }

    /// <summary>
    /// Tests that prefix matching is case insensitive.
    /// </summary>
    [Test]
    public async Task Lookup_IsCaseInsensitive()
    {
        var trie = CreateDefaultTrie();

        var result = trie.Lookup("FEAT: add new button");

        await Assert.That(result.Name).IsEqualTo(FeaturesCategory);
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

        await Assert.That(result.Name).IsEqualTo(FixesCategory);
    }

    /// <summary>
    /// Tests that the bug prefix also maps to Fixes.
    /// </summary>
    [Test]
    public async Task Lookup_WithBugPrefix_ReturnsFixes()
    {
        var trie = CreateDefaultTrie();

        var result = trie.Lookup("bug: handle edge case");

        await Assert.That(result.Name).IsEqualTo(FixesCategory);
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

        await Assert.That(trie.Count).IsEqualTo(ExpectedGroupCount);
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

        await Assert.That(groups).Count().IsEqualTo(ExpectedGroupCount);
        await Assert.That(groups[0].Category).IsEqualTo(FeaturesCategory);
        await Assert.That(groups[1].Category).IsEqualTo(FixesCategory);
        await Assert.That(groups[ExpectedGroupCount - 1].Category).IsEqualTo(DocumentationCategory);
    }

    /// <summary>
    /// Tests that the non-generic enumerator iterates the registered groups.
    /// </summary>
    [Test]
    public async Task NonGenericEnumerator_IteratesGroups()
    {
        var trie = CreateDefaultTrie();

        var enumerator = ((System.Collections.IEnumerable)trie).GetEnumerator();

        await Assert.That(enumerator.MoveNext()).IsTrue();
    }

    /// <summary>
    /// Creates a trie with Features, Fixes, and Documentation categories for testing.
    /// </summary>
    /// <returns>A configured <see cref="CategoryTrie"/> instance.</returns>
    private static CategoryTrie CreateDefaultTrie() => new(
        "Other",
        [
            new(FeaturesPriority, FeaturesCategory, FeatPrefixes),
            new(FixesPriority, FixesCategory, FixPrefixes),
            new(DocumentationPriority, DocumentationCategory, DocPrefixes)
        ]);
}
