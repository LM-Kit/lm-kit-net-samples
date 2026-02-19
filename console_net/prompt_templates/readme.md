# Prompt Templates with Logic

Interactive demo showcasing LM-Kit.NET's `PromptTemplate` engine for building dynamic, reusable prompts with variable substitution, conditionals, loops, filters, scoping, and custom helpers.

## Features

- Variable substitution with dot-notation for nested properties
- Filter chaining via pipe syntax (`|trim|upper|truncate:50`)
- Inline defaults for missing variables (`{{role:user}}`)
- Conditional blocks (`{{#if}}...{{#else}}...{{/if}}`, `{{#unless}}`)
- Loop blocks (`{{#each items}}...{{/each}}`) with `{{this}}`, `{{@index}}`, `{{@first}}`
- Scoping blocks (`{{#with user}}...{{/with}}`)
- Custom helper functions
- Variable introspection (list all referenced variables)
- Alternative syntaxes: Dollar (`${var}`) and Percent (`%var%`)
- Live chat with a system prompt built dynamically from a template

## Prerequisites

- .NET 8.0 or later
- LM-Kit.NET SDK
- Sufficient VRAM for the selected model (4-18 GB depending on model choice)

## How It Works

The demo has two parts:

1. **Template Showcase** (no model required): walks through each template feature interactively, showing the template source and rendered output side by side.

2. **Dynamic Chat**: asks you to configure an assistant (domain, language, verbosity) and builds a system prompt from a template at runtime. The configured assistant then runs as a live multi-turn chat.

## Usage

1. Run the application
2. Observe the template feature showcase
3. Select a language model for the live chat portion
4. Configure the assistant via template variables
5. Chat with the dynamically configured assistant

## Example Output

```
─── 1. Basic Variable Substitution ───
  Template: You are {{role}}. Help the user with {{topic}}.
  Result  : You are a senior C# developer. Help the user with async programming.

─── 2. Filters and Defaults ───
  Template: Welcome, {{name|trim|capitalize}}! Your role: {{role:user}}.
  Result  : Welcome, Alice! Your role: user.

─── 3. Conditionals (if / else) ───
  Template : {{#if premium}}...{{#else}}...{{/if}}
  premium=true  => You are a premium support agent. Provide detailed, in-depth answers.
  premium=false => You are a helpful assistant. Keep answers concise.
```

## Customization

- Modify the template strings to experiment with different prompt patterns
- Register custom global filters via `PromptTemplateFilters.Register()`
- Use `PromptTemplateOptions.StrictVariables = true` to catch missing variables early
- Switch syntax with `PromptTemplateSyntax.Dollar` or `PromptTemplateSyntax.Percent`
