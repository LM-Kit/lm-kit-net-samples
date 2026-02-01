# Multi-Turn Chat with Agent Skills

A comprehensive demo showcasing the **Agent Skills** feature in LM-Kit.NET. Agent Skills are reusable, shareable instruction sets bundled with resources that transform AI behavior for specific tasks.

## Features

- **Skill Activation**: Manually activate skills via slash commands
- **Auto-Selection**: LLM automatically selects skills based on user requests (SkillTool)
- **On-Demand Resources**: LLM loads templates/references during conversation (SkillResourceTool)
- **Remote Loading**: Load skills from URLs, ZIP archives, or GitHub repositories
- **Async APIs**: Non-blocking skill loading for production applications

## Bundled Skills

| Skill | Resources | Description |
|-------|-----------|-------------|
| `/api-designer` | 2 files | Designs RESTful APIs with OpenAPI 3.1 specs |
| `/git-commit-pro` | 1 file | Generates conventional commit messages from diffs |
| `/release-notes` | 2 files | Generates changelogs following Keep a Changelog |
| `/code-migrator` | 2 files | Guides framework migrations with checklists |
| `/prompt-engineer` | 1 file | Crafts optimized prompts using proven patterns |
| `/contract-analyzer` | 1 file | Analyzes software contracts for risks |

## Commands

### Skill Activation
| Command | Description |
|---------|-------------|
| `/<skill-name>` | Activate a skill (e.g., `/git-commit-pro`) |
| `/skills` | List all available skills with resource counts |
| `/info <skill>` | Show skill details including resources |
| `/resources <skill>` | View resource file contents |
| `/active` | Show currently active skill |
| `/deactivate` | Return to normal mode |

### LLM Tools
| Command | Description |
|---------|-------------|
| `/auto` | Toggle SkillTool (LLM can auto-select skills) |
| `/tools` | Toggle SkillResourceTool (LLM can load resources on-demand) |

### Remote Loading
| Command | Description |
|---------|-------------|
| `/remote` | Load a skill from URL (SKILL.md or .zip) |
| `/github owner/repo/path` | Load a skill from GitHub repository |
| `/cache` | Show skill cache information |

### Skill Management
| Command | Description |
|---------|-------------|
| `/create` | Create a new skill interactively |
| `/load` | Load a skill from a local directory |
| `/search` | Search skills by keyword |

## Quick Start

```bash
dotnet run
```

1. Select a model
2. Load a skill from GitHub: `/github LM-Kit/skills-library/code-review`
3. Activate: `/code-review`
4. Start chatting!

## Remote Skill Loading

### From URL

```
/remote
Enter skill URL: https://example.com/skills/my-skill/SKILL.md
```

Supports:
- Direct SKILL.md file URLs
- ZIP archives containing skill directories

### From GitHub

```
/github owner/repo/path/to/skill
```

Example:
```
/github LM-Kit/skills-library/skills/code-review
```

Resources in subdirectories (scripts/, references/, templates/) are automatically downloaded.

## Async APIs

For production applications, use async methods:

```csharp
// Async skill loading
var skill = await AgentSkill.LoadAsync("./skills/my-skill", cancellationToken);

// Async directory loading
int loaded = await registry.LoadFromDirectoryAsync("./skills", cancellationToken: ct);

// Async remote loading
await registry.LoadFromUrlAsync("https://example.com/skill.zip", cancellationToken: ct);

// Async GitHub loading
var skill = await registry.LoadFromGitHubAsync("owner", "repo", "path/to/skill");
```

## SkillRemoteLoader

```csharp
using (var loader = new SkillRemoteLoader())
{
    // Load from URL
    var skill = await loader.LoadFromUrlAsync("https://example.com/SKILL.md");
    
    // Load from ZIP
    var skills = await loader.LoadFromZipUrlAsync("https://example.com/skills.zip");
    
    // Load from GitHub
    var ghSkill = await loader.LoadFromGitHubAsync("owner", "repo", "skills/code-review");
    
    // Cache management
    var cacheInfo = loader.GetCacheInfo();
    Console.WriteLine($"Cached: {cacheInfo.SkillCount} skills ({cacheInfo.TotalSizeFormatted})");
    
    loader.ClearCache();
}
```

## Creating Skills

### Local Skills

```
my-skill/
  SKILL.md              # Required
  templates/            # Optional
  references/           # Optional
  scripts/              # Optional
```

### Publishing Skills

1. Create a GitHub repository
2. Add your skill directory with SKILL.md and resources
3. Share: `github.com/you/skills-repo/my-skill`

Others can load with:
```
/github you/skills-repo/my-skill
```

## Prerequisites

- .NET 8.0 or later
- LM-Kit.NET SDK
- Sufficient VRAM (3-11 GB depending on model)
- Internet connection (for remote loading)

## Learn More

- [LM-Kit.NET Documentation](https://docs.lm-kit.com)
- [Agent Skills Specification](https://agentskills.io)
- [LM-Kit Samples Repository](https://github.com/LM-Kit/lm-kit-net-samples)
