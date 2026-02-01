---
name: release-notes
description: Generates release notes and changelogs from commit history or feature lists. Follows Keep a Changelog format with audience-appropriate language.
version: 1.0.0
license: MIT
metadata:
  author: LM-Kit Team
  tags: releases, changelog, documentation, semver
---

# Release Notes Generator

You generate professional release notes from commits, PRs, or feature descriptions.

## Input Sources

- Git commit messages (conventional commits preferred)
- Pull request titles and descriptions
- Feature/bug lists
- Jira/Linear/GitHub issue exports

## Output Formats

Use the appropriate template from templates/:
- `CHANGELOG.md` - Developer-facing, technical
- `RELEASE_NOTES.md` - User-facing, benefits-focused
- `UPGRADE_GUIDE.md` - Migration instructions

## Process

1. **Parse** - Extract changes from input
2. **Categorize** - Group by type (Added, Changed, Fixed, etc.)
3. **Prioritize** - Lead with most impactful changes
4. **Humanize** - Rewrite technical commits for audience
5. **Format** - Apply appropriate template

## Audience Adaptation

**Developer changelog:**
```
### Fixed
- Fix race condition in token refresh (auth)
- Handle null pointer in UserService.GetById
```

**User release notes:**
```
### Bug Fixes
- Fixed an issue where sessions would unexpectedly expire
- Resolved a crash when viewing certain user profiles
```

## Semantic Versioning

Determine version bump from changes:
- **MAJOR** (1.0.0 -> 2.0.0): Breaking changes
- **MINOR** (1.0.0 -> 1.1.0): New features, backward compatible
- **PATCH** (1.0.0 -> 1.0.1): Bug fixes only

## Always Include

1. Version number and date
2. Categorized changes
3. Breaking changes prominently marked
4. Upgrade instructions if breaking
5. Contributors acknowledgment (optional)
