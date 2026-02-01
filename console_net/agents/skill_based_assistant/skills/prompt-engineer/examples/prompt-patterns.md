# Prompt Engineering Examples

## Data Extraction

### Vague Request
```
Extract info from this text
```

### Optimized Prompt
```
Extract the following from the text and return as JSON:
- company_name: string
- founding_year: number or null
- headquarters: string or null  
- key_people: array of {name, role}
- products: array of strings

If information is not present, use null.
Do not infer or guess missing values.

Text:
{input}
```

---

## Code Review

### Vague Request
```
Review this code
```

### Optimized Prompt
```
Review this code for:
1. Security vulnerabilities (OWASP Top 10)
2. Performance issues
3. Error handling gaps
4. Code style violations

For each issue found:
- Line number
- Severity: critical/high/medium/low
- Description
- Suggested fix with code example

If no issues in a category, state "None found".

Code:
```{language}
{code}
```
```

---

## Content Generation

### Vague Request
```
Write a blog post about AI
```

### Optimized Prompt
```
Write a technical blog post for software developers.

Topic: Practical applications of local LLMs in enterprise software
Audience: Senior developers, architects, CTOs
Tone: Professional but accessible, avoid marketing speak
Length: 800-1000 words

Structure:
1. Hook: Real problem this solves
2. Why now: What changed to make this viable
3. How it works: Technical explanation with code example
4. Trade-offs: Honest assessment
5. Getting started: Actionable first step

Do NOT include:
- Vague claims like "revolutionary" or "game-changing"
- Predictions about AI taking jobs
- References to ChatGPT or competitors
```

---

## Classification

### Vague Request
```
Categorize these support tickets
```

### Optimized Prompt
```
Classify each support ticket into exactly one category:

Categories:
- BILLING: Payment, invoices, refunds, subscriptions
- TECHNICAL: Bugs, errors, not working as expected
- ACCOUNT: Login, password, permissions, settings
- FEATURE: Requests for new functionality
- DOCS: Questions answered in documentation

Output format (one per line):
TICKET_ID: CATEGORY | confidence: high/medium/low

Example:
T001: BILLING | confidence: high
T002: TECHNICAL | confidence: medium

Tickets:
{tickets}
```

---

## Translation with Context

### Vague Request
```
Translate to French
```

### Optimized Prompt
```
Translate to French (France).

Context: Software documentation for developers
Register: Technical, formal
Preserve: Code terms (API, JSON, etc.), brand names

Handle ambiguity:
- "you" = "vous" (formal)
- Technical terms: keep English if no standard French equivalent

Source:
{text}

Output only the translation, no explanations.
```

---

## Structured Analysis

### Vague Request
```
Analyze this data
```

### Optimized Prompt
```
Analyze this sales data and provide:

1. SUMMARY (2-3 sentences)
   Overall trend and most notable finding

2. KEY METRICS
   - Total revenue
   - Average order value
   - Top 3 products by revenue
   - Month-over-month growth %

3. ANOMALIES
   Any unusual patterns or outliers worth investigating

4. RECOMMENDATIONS
   Top 3 actionable insights, prioritized by potential impact

Format numbers: $X,XXX.XX for currency, X.X% for percentages
Use tables where appropriate.

Data:
{data}
```
