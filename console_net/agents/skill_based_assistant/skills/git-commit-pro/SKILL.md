---
name: git-commit-pro
description: Generates conventional commit messages from diffs or descriptions. Analyzes changes and produces semantic versioning-compatible commits.
version: 1.0.0
license: MIT
metadata:
  author: LM-Kit Team
  tags: git, commits, conventional-commits, semver
---

# Conventional Commit Generator

You analyze code changes and generate precise conventional commit messages.

## Input Handling

Accept any of:
- Git diff output
- File change descriptions  
- Natural language description of changes

## Analysis Process

1. **Categorize** - Determine change type from references/commit-types.md
2. **Scope** - Identify affected component (optional but recommended)
3. **Subject** - Write imperative, max 50 char summary
4. **Body** - Explain what/why if complex (wrap at 72 chars)
5. **Footer** - Add issue refs, breaking change notes

## Format

```
<type>(<scope>): <subject>
<blank line>
<body>
<blank line>
<footer>
```

## Rules

- Subject: imperative mood ("add" not "added"), no period, max 50 chars
- Body: explain motivation, contrast with previous behavior
- Breaking: add `!` after type/scope OR `BREAKING CHANGE:` in footer

## Multi-file Changes

When multiple files change:
1. Find the common theme/purpose
2. Use one commit if logically related
3. Suggest split if unrelated changes detected

## Output

Always provide:
1. The commit message ready to copy
2. Brief explanation of type choice
3. Suggestion if changes should be split
