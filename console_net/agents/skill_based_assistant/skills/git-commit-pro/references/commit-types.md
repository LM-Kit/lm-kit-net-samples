# Conventional Commit Types

## Primary Types

| Type | When to Use | SemVer Impact | Example |
|------|-------------|---------------|---------|
| `feat` | New feature visible to user | MINOR bump | `feat(cart): add quantity selector` |
| `fix` | Bug fix visible to user | PATCH bump | `fix(auth): handle expired tokens` |

## Secondary Types (no version bump)

| Type | When to Use | Example |
|------|-------------|---------|
| `docs` | Documentation only | `docs: update installation guide` |
| `style` | Formatting, whitespace, semicolons | `style: fix linting errors` |
| `refactor` | Code change without feature/fix | `refactor(api): extract validation logic` |
| `perf` | Performance improvement | `perf(query): add database index` |
| `test` | Adding or fixing tests | `test(user): add registration tests` |
| `build` | Build system, dependencies | `build: upgrade to webpack 5` |
| `ci` | CI/CD configuration | `ci: add staging deploy workflow` |
| `chore` | Maintenance, tooling | `chore: update .gitignore` |
| `revert` | Revert previous commit | `revert: feat(cart): add quantity` |

## Common Scopes

```
api, ui, auth, db, core, config, deps, 
test, docs, build, ci, types, utils,
models, routes, middleware, services
```

## Breaking Changes

Two ways to indicate:

**Option 1: Exclamation mark**
```
feat(api)!: change response format to JSON:API
```

**Option 2: Footer**
```
feat(api): change response format

BREAKING CHANGE: Response now follows JSON:API spec.
Clients must update their parsers.
```

## Issue References

```
Fixes #123
Closes #456
Refs #789
```

## Examples by Scenario

**Adding a new endpoint:**
```
feat(api): add user preferences endpoint

GET/PUT /users/{id}/preferences for theme, 
notifications, and language settings.

Closes #234
```

**Fixing a null reference:**
```
fix(checkout): handle missing shipping address

Returns validation error instead of crashing
when user skips address step.

Fixes #567
```

**Refactoring without behavior change:**
```
refactor(services): extract email templating

Move email template logic from UserService
to dedicated EmailTemplateService.
No functional changes.
```

**Dependency update with breaking change:**
```
build(deps)!: upgrade to React 18

BREAKING CHANGE: Requires Node 16+.
Update createRoot API usage throughout.
```
