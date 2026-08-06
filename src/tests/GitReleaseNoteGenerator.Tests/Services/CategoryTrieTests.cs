// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using GitReleaseNoteGenerator.Services;

namespace GitReleaseNoteGenerator.Tests.Services;

/// <summary>Tests for <see cref="CategoryTrie"/>.</summary>
public class CategoryTrieTests
{
    /// <summary>The Features category name.</summary>
    private const string FeaturesCategory = "Features";

    /// <summary>The Fixes category name.</summary>
    private const string FixesCategory = "Fixes";

    /// <summary>The Documentation category name.</summary>
    private const string DocumentationCategory = "Documentation";

    /// <summary>The fallback category name for messages that match no prefix.</summary>
    private const string OtherCategory = "Other";

    /// <summary>Priority the trie derives for Features, which is registered first.</summary>
    private const int FeaturesPriority = 1;

    /// <summary>Priority the trie derives for Fixes, which is registered second.</summary>
    private const int FixesPriority = 2;

    /// <summary>The number of category groups configured in the test trie.</summary>
    private const int ExpectedGroupCount = 3;

    /// <summary>Prefixes that map to the Features category.</summary>
    private static readonly string[] FeatPrefixes = ["feat"];

    /// <summary>Prefixes that map to the Fixes category.</summary>
    private static readonly string[] FixPrefixes = ["fix", "bug"];

    /// <summary>Prefixes that map to the Documentation category.</summary>
    private static readonly string[] DocPrefixes = ["doc"];

    /// <summary>Tests that a message matching a prefix returns the correct category.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task LookupWithMatchingPrefixReturnsCorrectCategory()
    {
        var trie = CreateDefaultTrie();

        var result = trie.Lookup("feat: add new button");

        await Assert.That(result.Name).IsEqualTo(FeaturesCategory);
        await Assert.That(result.Priority).IsEqualTo(FeaturesPriority);
    }

    /// <summary>
    /// Tests that a category's priority is its registration order, so a category registered
    /// earlier outranks one registered later.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task LookupAssignsPriorityFromRegistrationOrder()
    {
        var trie = CreateDefaultTrie();

        var features = trie.Lookup("feat: add new button");
        var fixes = trie.Lookup("fix: resolve null reference");
        var documentation = trie.Lookup("doc: describe the option");

        await Assert.That(features.Priority).IsEqualTo(FeaturesPriority);
        await Assert.That(fixes.Priority).IsEqualTo(FixesPriority);
        await Assert.That(documentation.Priority).IsEqualTo(ExpectedGroupCount);
    }

    /// <summary>Tests that prefix matching is case insensitive.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task LookupIsCaseInsensitive()
    {
        var trie = CreateDefaultTrie();

        var result = trie.Lookup("FEAT: add new button");

        await Assert.That(result.Name).IsEqualTo(FeaturesCategory);
    }

    /// <summary>Tests that an unmatched message returns the Other category.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task LookupWithNoMatchReturnsOtherCategory()
    {
        var trie = CreateDefaultTrie();

        var result = trie.Lookup("random commit message");

        await Assert.That(result.Name).IsEqualTo(OtherCategory);
        await Assert.That(result.Priority).IsEqualTo(int.MaxValue);
    }

    /// <summary>Tests that the fix prefix is correctly matched.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task LookupWithFixPrefixReturnsFixes()
    {
        var trie = CreateDefaultTrie();

        var result = trie.Lookup("fix: resolve null reference");

        await Assert.That(result.Name).IsEqualTo(FixesCategory);
    }

    /// <summary>Tests that the bug prefix also maps to Fixes.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task LookupWithBugPrefixReturnsFixes()
    {
        var trie = CreateDefaultTrie();

        var result = trie.Lookup("bug: handle edge case");

        await Assert.That(result.Name).IsEqualTo(FixesCategory);
    }

    /// <summary>Tests that the indexer works the same as Lookup.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task IndexerReturnsSameAsLookup()
    {
        var trie = CreateDefaultTrie();

        var indexerResult = trie["feat: something"];
        var lookupResult = trie.Lookup("feat: something");

        await Assert.That(indexerResult).IsEqualTo(lookupResult);
    }

    /// <summary>Tests that Count reflects the number of category groups.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task CountReturnsNumberOfGroups()
    {
        var trie = CreateDefaultTrie();

        await Assert.That(trie.Count).IsEqualTo(ExpectedGroupCount);
    }

    /// <summary>Tests that an empty message returns Other.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task LookupWithEmptyMessageReturnsOther()
    {
        var trie = CreateDefaultTrie();

        var result = trie.Lookup(string.Empty);

        await Assert.That(result.Name).IsEqualTo(OtherCategory);
    }

    /// <summary>Tests that the groups are exposed in registration order.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task GroupsReturnsAllGroupsInRegistrationOrder()
    {
        var trie = CreateDefaultTrie();

        var groups = trie.Groups;

        await Assert.That(groups).Count().IsEqualTo(ExpectedGroupCount);
        await Assert.That(groups[0].Category).IsEqualTo(FeaturesCategory);
        await Assert.That(groups[1].Category).IsEqualTo(FixesCategory);
        await Assert.That(groups[ExpectedGroupCount - 1].Category).IsEqualTo(DocumentationCategory);
    }

    /// <summary>Tests that a group carries the prefixes it was registered with.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task GroupsCarryTheRegisteredPrefixes()
    {
        var trie = CreateDefaultTrie();

        var fixes = trie.Groups[1];

        await Assert.That(fixes.Prefixes).IsEquivalentTo(FixPrefixes);
    }

    /// <summary>
    /// Creates a trie with Features, Fixes, and Documentation categories for testing. Declaration
    /// order here is what gives each category the priority asserted by these tests.
    /// </summary>
    /// <returns>A configured <see cref="CategoryTrie"/> instance.</returns>
    private static CategoryTrie CreateDefaultTrie() => new(
        OtherCategory,
        [
            new(FeaturesCategory, FeatPrefixes),
            new(FixesCategory, FixPrefixes),
            new(DocumentationCategory, DocPrefixes)
        ]);
}
