// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Licensed under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.CommandLine;

using GitReleaseNoteGenerator.Infrastructure;

namespace GitReleaseNoteGenerator.Commands;

/// <summary>
/// Pure logic for reading, validating, and resolving generate-command arguments. Kept free of
/// console and process side effects so it can be unit tested directly.
/// </summary>
internal static class CommandArgumentResolver
{
    /// <summary>
    /// Reads the raw command values from the parse result, falling back to the GitHub Actions
    /// environment for the token, owner, and repository.
    /// </summary>
    /// <param name="parseResult">The parse result from the command-line invocation.</param>
    /// <param name="options">The configured command options.</param>
    /// <returns>The raw command values.</returns>
    public static GenerateCommandValues ReadValues(ParseResult parseResult, GenerateCommandOptions options)
    {
        ArgumentNullException.ThrowIfNull(parseResult);
        ArgumentNullException.ThrowIfNull(options);

        return new(
            parseResult.GetValue(options.TokenOption) ?? GitHubActionEnvironment.Token,
            parseResult.GetValue(options.OwnerOption) ?? GitHubActionEnvironment.RepositoryOwner,
            parseResult.GetValue(options.RepoOption) ?? GitHubActionEnvironment.RepositoryName,
            parseResult.GetValue(options.BaseRefOption),
            parseResult.GetValue(options.HeadRefOption),
            parseResult.GetValue(options.VersionOption),
            parseResult.GetValue(options.OutputFileOption),
            parseResult.GetValue(options.GitHubOutputOption),
            parseResult.GetValue(options.OutputNameOption) ?? CommandOptionsFactory.DefaultOutputName);
    }

    /// <summary>
    /// Validates the command values that must be provided by arguments or environment variables.
    /// </summary>
    /// <param name="values">The command values to validate.</param>
    /// <returns>The validation outcome.</returns>
    public static CommandValidationStatus Validate(GenerateCommandValues values)
    {
        ArgumentNullException.ThrowIfNull(values);

        if (string.IsNullOrEmpty(values.Token))
        {
            return CommandValidationStatus.TokenMissing;
        }

        if (string.IsNullOrEmpty(values.Owner) || string.IsNullOrEmpty(values.Repo))
        {
            return CommandValidationStatus.RepositoryMissing;
        }

        if (string.IsNullOrEmpty(values.Version))
        {
            return CommandValidationStatus.VersionMissing;
        }

        return CommandValidationStatus.Valid;
    }

    /// <summary>
    /// Maps validated command values into command arguments. The token, owner, repo, and version
    /// must already have been confirmed present by <see cref="Validate"/>.
    /// </summary>
    /// <param name="values">The validated command values.</param>
    /// <returns>The resolved command arguments.</returns>
    public static GenerateCommandArguments CreateArguments(GenerateCommandValues values)
    {
        ArgumentNullException.ThrowIfNull(values);

        return new(
            values.Token!,
            values.Owner!,
            values.Repo!,
            values.BaseRef,
            values.HeadRef,
            values.Version!,
            values.OutputFile,
            values.GitHubOutput,
            values.OutputName);
    }
}
