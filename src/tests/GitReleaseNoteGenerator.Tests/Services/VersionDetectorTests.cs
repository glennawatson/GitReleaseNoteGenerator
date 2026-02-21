// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using GitReleaseNoteGenerator.Services;

namespace GitReleaseNoteGenerator.Tests.Services;

/// <summary>
/// Tests for <see cref="VersionDetector"/>.
/// </summary>
public class VersionDetectorTests
{
    /// <summary>
    /// Tests parsing a typical nbgv get-version output.
    /// </summary>
    [Test]
    public async Task ParseNuGetPackageVersion_WithValidOutput_ReturnsVersion()
    {
        var output = """
            AssemblyVersion:      1.0.0.0
            AssemblyFileVersion:  1.0.42.12345
            NuGetPackageVersion:  1.0.42-g1234567890
            SemVer2:              1.0.42-g1234567890
            """;

        var version = VersionDetector.ParseNuGetPackageVersion(output);

        await Assert.That(version).IsEqualTo("1.0.42-g1234567890");
    }

    /// <summary>
    /// Tests parsing when NuGetPackageVersion is not present.
    /// </summary>
    [Test]
    public async Task ParseNuGetPackageVersion_WithMissingVersion_ReturnsNull()
    {
        var output = """
            AssemblyVersion:      1.0.0.0
            SemVer2:              1.0.42
            """;

        var version = VersionDetector.ParseNuGetPackageVersion(output);

        await Assert.That(version).IsNull();
    }

    /// <summary>
    /// Tests parsing empty output.
    /// </summary>
    [Test]
    public async Task ParseNuGetPackageVersion_WithEmptyOutput_ReturnsNull()
    {
        var version = VersionDetector.ParseNuGetPackageVersion(string.Empty);

        await Assert.That(version).IsNull();
    }

    /// <summary>
    /// Tests that the colon in the value doesn't break parsing (Split on first colon only).
    /// </summary>
    [Test]
    public async Task ParseNuGetPackageVersion_WithColonInValue_ParsesCorrectly()
    {
        var output = "NuGetPackageVersion:  1.0.0-beta:special";

        var version = VersionDetector.ParseNuGetPackageVersion(output);

        await Assert.That(version).IsEqualTo("1.0.0-beta:special");
    }

    /// <summary>
    /// Tests case insensitive matching of the key.
    /// </summary>
    [Test]
    public async Task ParseNuGetPackageVersion_IsCaseInsensitive()
    {
        var output = "nugetpackageversion:  2.0.0";

        var version = VersionDetector.ParseNuGetPackageVersion(output);

        await Assert.That(version).IsEqualTo("2.0.0");
    }
}
