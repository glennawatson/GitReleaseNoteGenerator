# GitReleaseNoteGenerator

A .NET global tool that generates categorized release notes from git commit history using the GitHub API.

## Installation

```bash
dotnet tool install -g GitReleaseNoteGenerator
```

## Quick Start

```bash
# Generate release notes for the latest release
git-release-notes --token <GITHUB_PAT> --owner myorg --repo myrepo --release-version v2.0.0

# Auto-detect version via NBGV, write to file
git-release-notes --token <GITHUB_PAT> --owner myorg --repo myrepo --output-file release-notes.md

# Use with GITHUB_OUTPUT in CI
git-release-notes --github-output --output-name changelog
```

## CLI Reference

| Option | Type | Default | Description |
|---|---|---|---|
| `--token` | string | `GITHUB_TOKEN` env var | GitHub personal access token |
| `--owner` | string | From `GITHUB_REPOSITORY` env | Repository owner |
| `--repo` | string | From `GITHUB_REPOSITORY` env | Repository name |
| `--base-ref` | string | Latest release tag | Base ref to compare from |
| `--head-ref` | string | Default branch | Head ref to compare to |
| `--release-version` | string | Auto-detect via NBGV | Version string for the heading |
| `--output-file` | path | _(none)_ | Write release notes to a file |
| `--github-output` | flag | `false` | Write to `GITHUB_OUTPUT` |
| `--output-name` | string | `changelog` | Variable name for `GITHUB_OUTPUT` |

## Commit Prefix Categories

Commits are categorized by their conventional-commit-style prefix:

| Prefix | Category | Emoji |
|---|---|---|
| `break` | Breaking Changes | :boom: |
| `feat` | Features | :sparkles: |
| `refactor` | Refactoring | :recycle: |
| `fix`, `bug` | Fixes | :bug: |
| `perf` | Performance | :zap: |
| `housekeeping`, `chore`, `update` | General Changes | :broom: |
| `test` | Tests | :white_check_mark: |
| `doc` | Documentation | :memo: |
| `style` | Style Changes | :nail_care: |
| `dep` | Dependencies | :package: |

Commits from `dependabot[bot]` and `renovate[bot]` are automatically categorized as Dependencies.

## GitHub Actions Usage

```yaml
name: Create Release
on:
  push:
    tags: ['v*']
jobs:
  release:
    runs-on: ubuntu-latest
    permissions:
      contents: write
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'
      - run: dotnet tool install -g GitReleaseNoteGenerator
      - name: Generate Release Notes
        env:
          GITHUB_TOKEN: ${{ github.token }}
        run: git-release-notes --release-version ${{ github.ref_name }} --output-file release-notes.md
      - name: Create GitHub Release
        env:
          GH_TOKEN: ${{ github.token }}
        run: gh release create "${{ github.ref_name }}" --title "${{ github.ref_name }}" --notes-file release-notes.md
```

## Migration from ChangeLog Action

If you are migrating from the [ChangeLog GitHub Action](https://github.com/glennawatson/ChangeLog):

1. Replace the `glennawatson/ChangeLog@...` step with the dotnet tool install and run steps shown above.
2. The commit prefix categories and emoji mappings are the same.
3. Use `--output-file` with `gh release create --notes-file` instead of action outputs.
4. The `GITHUB_TOKEN` environment variable is read automatically when `--token` is not specified.

## License

MIT
