// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

using GitReleaseNoteGenerator.Models;
using GitReleaseNoteGenerator.Services;

namespace GitReleaseNoteGenerator.Tests.Services;

/// <summary>Tests for <see cref="AuthorMention"/>, which decides whether a contributor is rendered as an @mention or as literal text.</summary>
public class AuthorMentionTests
{
    /// <summary>The maximum length of a GitHub login.</summary>
    private const int MaxLoginLength = 39;

    /// <summary>One character beyond the maximum length of a GitHub login.</summary>
    private const int OverlongLoginLength = MaxLoginLength + 1;

    /// <summary>Tests that a confirmed GitHub login is rendered as an @mention.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task AppendWithConfirmedLoginWritesMention() =>
        await Assert.That(RenderLogin("octocat")).IsEqualTo("@octocat");

    /// <summary>Tests that a login containing a hyphen is still mentionable.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task AppendWithHyphenatedLoginWritesMention() =>
        await Assert.That(RenderLogin("dotnet-foundation")).IsEqualTo("@dotnet-foundation");

    /// <summary>
    /// Tests that a display name which happens to be shaped like a valid login is still not
    /// mentioned. This is the failure shape that no amount of inspecting the text can catch: a
    /// commit recording only the name "Lukas" would otherwise credit the stranger who holds that
    /// account.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task AppendWithLoginShapedDisplayNameWritesLiteralText()
    {
        var result = RenderDisplayName("Lukas");

        await Assert.That(result).IsEqualTo("Lukas");
        await Assert.That(result).DoesNotContain("@");
    }

    /// <summary>
    /// Tests that a display name carrying accented characters is written as literal text. GitHub
    /// logins are ASCII-only, so such a mention could never resolve to an account.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task AppendWithAccentedDisplayNameWritesLiteralTextNotMention()
    {
        const string DisplayName = "Tést Üser dé Éxample";

        var result = RenderDisplayName(DisplayName);

        await Assert.That(result).IsEqualTo(DisplayName);
        await Assert.That(result).DoesNotContain("@");
    }

    /// <summary>
    /// Tests that a bot login is written as literal text with its brackets escaped, even though it
    /// is a genuine login. Left raw, Markdown consumes the "[bot]" as a link and "@renovate"
    /// autolinks to an unrelated user.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task AppendWithBotLoginEscapesBracketsAndOmitsMention() =>
        await Assert.That(RenderLogin("renovate[bot]")).IsEqualTo(@"renovate\[bot\]");

    /// <summary>Tests that Markdown emphasis characters in a display name are escaped.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task AppendWithEmphasisCharactersEscapesThem() =>
        await Assert.That(RenderDisplayName("some_user*name")).IsEqualTo(@"some\_user\*name");

    /// <summary>Tests that a name shaped like an HTML tag is escaped rather than swallowed by the renderer.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task AppendWithAngleBracketsEscapesThem() =>
        await Assert.That(RenderDisplayName("<script>")).IsEqualTo(@"\<script\>");

    /// <summary>Tests that a name containing spaces is not mentioned, since logins cannot contain spaces.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task AppendWithSpacesWritesLiteralText() =>
        await Assert.That(RenderDisplayName("John Doe")).IsEqualTo("John Doe");

    /// <summary>
    /// Tests that an identifier exceeding GitHub's login length limit is not mentioned even when
    /// resolution claimed it was a login, since no account can carry that name.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task AppendWithOverlongLoginWritesLiteralText()
    {
        var overlong = new string('a', OverlongLoginLength);

        await Assert.That(RenderLogin(overlong)).IsEqualTo(overlong);
    }

    /// <summary>Tests that a login of exactly the maximum length is still mentioned.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task AppendWithMaximumLengthLoginWritesMention()
    {
        var maximum = new string('a', MaxLoginLength);

        await Assert.That(RenderLogin(maximum)).IsEqualTo($"@{maximum}");
    }

    /// <summary>Tests that a leading hyphen disqualifies an identifier from being mentioned.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task AppendWithLeadingHyphenWritesLiteralText() =>
        await Assert.That(RenderLogin("-nope")).IsEqualTo("-nope");

    /// <summary>Tests that a trailing hyphen disqualifies an identifier from being mentioned.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task AppendWithTrailingHyphenWritesLiteralText() =>
        await Assert.That(RenderLogin("nope-")).IsEqualTo("nope-");

    /// <summary>Tests that consecutive hyphens disqualify an identifier from being mentioned.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task AppendWithConsecutiveHyphensWritesLiteralText() =>
        await Assert.That(RenderLogin("a--b")).IsEqualTo("a--b");

    /// <summary>Tests that an empty identifier is not mentioned and contributes nothing.</summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Test]
    public async Task AppendWithEmptyIdentifierWritesNothing() =>
        await Assert.That(RenderLogin(string.Empty)).IsEqualTo(string.Empty);

    /// <summary>Renders an identity that resolution confirmed as a GitHub login.</summary>
    /// <param name="login">The login to render.</param>
    /// <returns>The rendered Markdown fragment.</returns>
    private static string RenderLogin(string login) => Render(ContributorIdentity.ForLogin(login));

    /// <summary>Renders an identity that resolution could only pin down to a display name.</summary>
    /// <param name="name">The display name to render.</param>
    /// <returns>The rendered Markdown fragment.</returns>
    private static string RenderDisplayName(string name) => Render(ContributorIdentity.ForDisplayName(name));

    /// <summary>Renders a single identity through <see cref="AuthorMention.Append"/>.</summary>
    /// <param name="author">The contributor identity to render.</param>
    /// <returns>The rendered Markdown fragment.</returns>
    private static string Render(ContributorIdentity author)
    {
        var builder = new StringBuilder();
        AuthorMention.Append(builder, author);
        return builder.ToString();
    }
}
