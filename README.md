# GitReleaseNoteGenerator

A .NET global tool that generates categorized release notes from git commit history using the GitHub API.

## Requirements

The tool ships builds for .NET 10 and .NET 11, so either runtime must be installed.

## Installation

```bash
dotnet tool install -g GitReleaseNoteGenerator
```

## Quick Start

```bash
# Generate release notes for a version
git-release-notes --token <GITHUB_PAT> --owner myorg --repo myrepo --release-version v2.0.0

# Write the notes to a file
git-release-notes --token <GITHUB_PAT> --owner myorg --repo myrepo --release-version v2.0.0 --output-file release-notes.md

# Write the notes to GITHUB_OUTPUT in CI
git-release-notes --token <GITHUB_PAT> --owner myorg --repo myrepo --release-version v2.0.0 --github-output --output-name changelog
```

> **Version:** `--release-version` is required. Compute it in your pipeline — for example with [Nerdbank.GitVersioning (`nbgv`)](https://github.com/dotnet/Nerdbank.GitVersioning) or [MinVer](https://github.com/adamralph/minver) — and pass the result in.

## CLI Reference

| Option | Type | Default | Description |
|---|---|---|---|
| `--token` | string | `GITHUB_TOKEN` env var | GitHub personal access token |
| `--owner` | string | From `GITHUB_REPOSITORY` env | Repository owner |
| `--repo` | string | From `GITHUB_REPOSITORY` env | Repository name |
| `--base-ref` | string | Latest release tag | Base ref to compare from |
| `--head-ref` | string | Default branch | Head ref to compare to |
| `--release-version` | string | **required** | Version string for the heading |
| `--tag-prefix` | string | _(none)_ | Select the most recently published stable release whose tag starts with this prefix |
| `--release-tag` | string | Inferred from version | Exact new tag for changelog links; overrides prefix inference |
| `--output-file` | path | _(none)_ | Write release notes to a file |
| `--github-output` | flag | `false` | Write to `GITHUB_OUTPUT` |
| `--output-name` | string | `changelog` | Variable name for `GITHUB_OUTPUT` |

## Independent Release Streams

Repositories can publish several package families with separate versions. Use `--tag-prefix` to
select a family's baseline. Prefix matching is exact and case-sensitive. The baseline is the most
recently published matching stable release, excluding drafts, pre-releases and the new release's
own tag. All release pages are searched. This selects by publication time, not semantic version.

An explicit `--base-ref` takes priority. Use it for backports or when comparing pre-release
channels. If the stream has no published stable release, notes include all history reachable from
the selected head. They do not fall back to another family's release.

The new changelog tag defaults to the prefix plus `--release-version`. A version that already
starts with the prefix is used unchanged. `--release-tag` overrides this for custom tag names.
Pass the built commit as `--head-ref` so the notes describe the source that was packaged.

For example, when both SourceGenerators and Binding ship, generate a separate notes file for
each release, using the same built commit and each family's own version:

```bash
git-release-notes --release-version 4.3.0 --tag-prefix v --release-tag v4.3.0 --head-ref BUILT_SHA --output-file sourcegenerators-notes.md
git-release-notes --release-version 9.2.0 --tag-prefix binding-v --release-tag binding-v9.2.0 --head-ref BUILT_SHA --output-file binding-notes.md
```

In GitHub Actions, use distinct `--output-name` values such as `sourcegenerators_notes` and
`binding_notes` when both invocations write to `GITHUB_OUTPUT`. Give the GitHub releases titles
that include the family and version. The release workflow chooses whether one or both families
ship; the generator only produces notes. It collects every commit in each comparison range and
does not filter by package, path, PR label or commit scope.

Existing invocations remain valid. Without `--tag-prefix`, automatic baseline selection uses the
repository's latest release. Without `--release-tag` or `--tag-prefix`, changelog tag inference
keeps the existing bare-version and `v`-prefix behaviour.

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

## How Commits Are Collected

- With a base ref (the latest release tag by default), the commits come from the GitHub compare API. It is read page by page until every commit in the range is collected.
- With no releases yet, the whole history reachable from the head ref is used.
- Contributors who already appear in the history reachable from the base ref are not listed as new contributors.
- Rate limits are waited out for as long as GitHub asks (`x-ratelimit-reset` or `Retry-After`). Server errors and network failures are retried up to three times with exponential backoff.

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
      - uses: actions/checkout@v7
        with:
          fetch-depth: 0
      - uses: actions/setup-dotnet@v6
        with:
          dotnet-version: '10.0.x'
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
