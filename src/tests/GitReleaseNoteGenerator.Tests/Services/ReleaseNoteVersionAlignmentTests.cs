// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using GitReleaseNoteGenerator.Services;

namespace GitReleaseNoteGenerator.Tests.Services;

/// <summary>Tests for <see cref="ReleaseNoteGenerator.AlignVersionWithBaseRefPrefix"/>, which keeps the generated changelog link pointed at a tag that actually exists.</summary>
public class ReleaseNoteVersionAlignmentTests
{
    /// <summary>A bare (unprefixed) release version used across the alignment tests.</summary>
    private const string BareVersion = "10.0.0";

    /// <summary>Tests that a bare version is given the base ref's "v" prefix so the compare link resolves.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task AlignVersionWithBaseRefPrefix_VPrefixedBaseRefAndBareVersion_AddsPrefix()
    {
        var result = ReleaseNoteGenerator.AlignVersionWithBaseRefPrefix(BareVersion, "v9.0.0");

        await Assert.That(result).IsEqualTo("v10.0.0");
    }

    /// <summary>Tests that a version already carrying the prefix is left untouched (no double "v").</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task AlignVersionWithBaseRefPrefix_VPrefixedBaseRefAndVersion_LeavesUnchanged()
    {
        var result = ReleaseNoteGenerator.AlignVersionWithBaseRefPrefix("v10.0.0", "v9.0.0");

        await Assert.That(result).IsEqualTo("v10.0.0");
    }

    /// <summary>Tests that a bare base ref leaves a bare version untouched (no prefix to infer).</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task AlignVersionWithBaseRefPrefix_BareBaseRefAndVersion_LeavesUnchanged()
    {
        var result = ReleaseNoteGenerator.AlignVersionWithBaseRefPrefix(BareVersion, "9.0.0");

        await Assert.That(result).IsEqualTo(BareVersion);
    }

    /// <summary>Tests that a null base ref (no previous release) leaves the version untouched.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task AlignVersionWithBaseRefPrefix_NullBaseRef_LeavesUnchanged()
    {
        var result = ReleaseNoteGenerator.AlignVersionWithBaseRefPrefix(BareVersion, null);

        await Assert.That(result).IsEqualTo(BareVersion);
    }

    /// <summary>
    /// Tests that a base ref that merely starts with the letter "v" but is not a version tag
    /// (for example, a "vnext" branch) does not trigger prefixing.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task AlignVersionWithBaseRefPrefix_NonVersionVBaseRef_LeavesUnchanged()
    {
        var result = ReleaseNoteGenerator.AlignVersionWithBaseRefPrefix(BareVersion, "vnext");

        await Assert.That(result).IsEqualTo(BareVersion);
    }

    /// <summary>Tests that an uppercase "V" prefix on the base ref is preserved when applied to the version.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task AlignVersionWithBaseRefPrefix_UppercaseVBaseRef_PreservesCase()
    {
        var result = ReleaseNoteGenerator.AlignVersionWithBaseRefPrefix(BareVersion, "V9.0.0");

        await Assert.That(result).IsEqualTo("V10.0.0");
    }
}
