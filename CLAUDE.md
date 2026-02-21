# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Test Commands

This project uses **Microsoft Testing Platform (MTP)** with the **TUnit** testing framework. Test commands differ significantly from traditional VSTest.

See: https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test?tabs=dotnet-test-with-mtp

### Prerequisites

```bash
# Check .NET installation
dotnet --info

# Restore NuGet packages
dotnet restore src/GitReleaseNoteGenerator.slnx
```

**Note**: This repository uses **SLNX** (XML-based solution format) instead of the legacy SLN format.

### Build Commands

Building can be done from the repository root:

```bash
# Build the solution
dotnet build src/GitReleaseNoteGenerator.slnx

# Build in Release mode
dotnet build src/GitReleaseNoteGenerator.slnx -c Release
```

### Test Commands (Microsoft Testing Platform)

**CRITICAL:** The working directory must be `./src` when running tests. The `global.json` that configures MTP lives in `src/` and won't be picked up from the repository root.

**IMPORTANT:**
- Do NOT use `--no-build` flag when running tests. Always build before testing to ensure all code changes are compiled.

```bash
# MUST cd to src/ first - global.json with MTP config lives here
cd src

# Run all tests in the solution
dotnet test --solution GitReleaseNoteGenerator.slnx -c Release

# Run all tests in a specific project
dotnet test --project tests/GitReleaseNoteGenerator.Tests/GitReleaseNoteGenerator.Tests.csproj -c Release

# Run a single test method using treenode-filter
dotnet test --project tests/GitReleaseNoteGenerator.Tests/GitReleaseNoteGenerator.Tests.csproj --treenode-filter "/*/*/*/MyTestMethod"

# Run all tests in a specific class
dotnet test --project tests/GitReleaseNoteGenerator.Tests/GitReleaseNoteGenerator.Tests.csproj --treenode-filter "/*/*/CommitCategorizerTests/*"

# Run tests in a specific namespace
dotnet test --project tests/GitReleaseNoteGenerator.Tests/GitReleaseNoteGenerator.Tests.csproj --treenode-filter "/*/GitReleaseNoteGenerator.Tests.Services/*/*"

# Run tests with code coverage
dotnet test --solution GitReleaseNoteGenerator.slnx --coverage --coverage-output-format cobertura

# Run tests with detailed output
dotnet test --solution GitReleaseNoteGenerator.slnx --output Detailed

# List all available tests without running them
dotnet test --project tests/GitReleaseNoteGenerator.Tests/GitReleaseNoteGenerator.Tests.csproj --list-tests

# Fail fast (stop on first failure)
dotnet test --solution GitReleaseNoteGenerator.slnx --fail-fast

# Combine options: coverage + detailed output
dotnet test --solution GitReleaseNoteGenerator.slnx --coverage --coverage-output-format cobertura --output Detailed
```

### TUnit Treenode-Filter Syntax

The `--treenode-filter` follows the pattern: `/{AssemblyName}/{Namespace}/{ClassName}/{TestMethodName}`

- Single test: `--treenode-filter "/*/*/*/MyTestMethod"`
- All tests in class: `--treenode-filter "/*/*/MyClassName/*"`
- All tests in namespace: `--treenode-filter "/*/MyNamespace/*/*"`
- Filter by property: `--treenode-filter "/*/*/*/*[Category=Integration]"`

**Note:** Use single asterisks (`*`) to match segments. Double asterisks (`/**`) are not supported in treenode-filter.

### Key TUnit Command-Line Flags

- `--treenode-filter` - Filter tests by path pattern or properties
- `--list-tests` - Display available tests without running
- `--fail-fast` - Stop after first failure
- `--maximum-parallel-tests` - Limit concurrent execution
- `--coverage` - Enable Microsoft Code Coverage
- `--coverage-output-format` - Set coverage format (cobertura, xml, coverage)
- `--report-trx` - Generate TRX format reports
- `--output` - Control verbosity (Normal or Detailed)
- `--disable-logo` - Remove TUnit logo display

See https://tunit.dev/docs/reference/command-line-flags for complete TUnit flag reference.

### Key Configuration Files

- `src/global.json` - Specifies `"Microsoft.Testing.Platform"` as the test runner
- `src/testconfig.json` - Configures test execution and code coverage (Cobertura format)
- `src/Directory.Build.props` - Enables `TestingPlatformDotnetTestSupport` for test projects
- `version.json` - Nerdbank.GitVersioning configuration

## Architecture Overview

GitReleaseNoteGenerator is a .NET global tool that generates categorized release notes from git commit history using the GitHub API. It replaces the legacy TypeScript ChangeLog GitHub Action.

### Project Structure

```
src/
  GitReleaseNoteGenerator/          # Main tool project (PackAsTool)
    Commands/GenerateCommand.cs     # System.CommandLine root command
    Services/
      ReleaseNoteGenerator.cs       # Core orchestrator (GitHub API calls)
      CategoryTrie.cs               # Prefix trie for commit message categorization
      CommitCategorizer.cs          # Commit -> category mapping + grouping
      AuthorExtractor.cs            # Author/co-author extraction & normalization
      VersionDetector.cs            # Cross-platform NBGV integration
      GitHubClientFactory.cs        # Authenticated GitHubClient factory
    Infrastructure/
      OutputWriter.cs               # stdout, file, GITHUB_OUTPUT writing
      RetryHandler.cs               # Polly retry for GitHub API
      GitHubActionEnvironment.cs    # GitHub Actions env var reader
    Models/
      ReleaseNoteOptions.cs         # CLI options model
      CategorizedCommit.cs          # Commit with category
      CategoryDefinition.cs         # Category definition record
    Program.cs                      # Entry point
  tests/GitReleaseNoteGenerator.Tests/
    Services/                       # Unit tests for pure logic classes
```

### Key Dependencies

- **Octokit 14.0.0** - GitHub API client (classic, not Kiota-based)
- **System.CommandLine 2.0.3** - CLI parsing (stable release, NOT beta)
- **Polly.Core** - Retry/resilience for API calls
- **Nerdbank.GitVersioning** - Version detection
- **TUnit** - Test framework (includes MS Test SDK and code coverage)

### Key Patterns

- **LoggerMessage source generation** - All logging uses `[LoggerMessage]` attribute for high-perf source-generated delegates
- **CategoryTrie** - Prefix tree for O(m) commit message categorization where m = prefix length
- **Polly retry pipeline** - Handles rate limits (waits for reset), 5xx errors, timeouts
- **GITHUB_OUTPUT heredoc** - Uses unique GUID delimiters to prevent content collisions

## Code Style & Quality Requirements

### Zero Warning Policy

This project enforces **zero warnings**. All analyzer warnings must be resolved, not suppressed.

### Style Enforcement

- EditorConfig rules (`.editorconfig`) - comprehensive C# formatting and naming conventions
- StyleCop Analyzers - builds fail on violations
- Roslynator Analyzers - additional code quality rules
- Analysis level: latest with all rules enabled (`AllEnabledByDefault`)
- `WarningsAsErrors`: nullable

### C# Style Rules

- **Braces:** Allman style (each brace on new line)
- **Indentation:** 4 spaces, no tabs
- **Fields:** `_camelCase` for private/internal, `readonly` where possible
- **Visibility:** Always explicit
- **Namespaces:** File-scoped preferred, imports outside namespace, sorted
- **Types:** Use keywords (`int`, `string`) not BCL types
- **Modern C#:** Nullable reference types, pattern matching, collection expressions, file-scoped namespaces
- **Avoid `this.`** unless necessary
- **Use `nameof()`** instead of string literals
- **Use `var`** when it improves readability
- **XML documentation:** Required on ALL elements regardless of visibility - fields, methods, parameters, properties, return values
- **StringComparison:** Always use explicit `StringComparison` overloads for `IndexOf`, `Replace`, `Contains`, etc.
- **Logging:** Use `[LoggerMessage]` source generation, never direct `logger.LogXxx()` calls

### Test Style

- TUnit framework with `[Test]` attribute and `await Assert.That(...)` assertions
- No mocking frameworks - test pure logic classes with concrete test data
- Test method naming: `MethodName_Scenario_ExpectedResult`
- Tests in `src/tests/` directory, matching project namespace structure

## Important Notes

- **No shallow clones:** Repository requires full clone for Nerdbank.GitVersioning
- **Required .NET SDKs:** .NET 8.0 and 10.0 (both LTS targets)
- **SLNX Format:** Uses modern XML-based solution format
- **PackAsTool:** Main project is distributed as a dotnet global tool (`git-release-notes`)
- **InternalsVisibleTo:** Test project can access `internal` members of the main project
