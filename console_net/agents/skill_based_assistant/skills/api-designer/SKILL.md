---
name: api-designer
description: Designs RESTful APIs with OpenAPI 3.1 specs, following industry best practices. Includes templates and naming conventions.
version: 1.0.0
license: MIT
metadata:
  author: LM-Kit Team
  tags: api, openapi, rest, design
---

# REST API Designer

You design professional REST APIs using OpenAPI 3.1 specification.

## Process

1. **Understand the domain** - Identify resources and relationships
2. **Design endpoints** - Use templates/openapi-template.yaml as base
3. **Apply conventions** - Follow references/rest-conventions.md
4. **Add schemas** - Define request/response models
5. **Document** - Add descriptions, examples, error codes

## Resource Naming Rules

- Use **plural nouns**: `/users`, `/orders`, `/products`
- Use **kebab-case**: `/user-profiles`, `/order-items`
- Nest for relationships: `/users/{id}/orders`
- Max 2 levels deep: avoid `/a/{id}/b/{id}/c/{id}/d`

## HTTP Methods

| Method | Usage | Idempotent | Response |
|--------|-------|------------|----------|
| GET | Read | Yes | 200 + body |
| POST | Create | No | 201 + Location |
| PUT | Replace | Yes | 200 or 204 |
| PATCH | Update | No | 200 + body |
| DELETE | Remove | Yes | 204 |

## Required for Every Endpoint

1. **Summary** - One line description
2. **OperationId** - Unique, camelCase (e.g., `getUserById`)
3. **Tags** - Group by resource
4. **Responses** - At minimum: success + 400 + 401 + 404 + 500
5. **Examples** - Realistic sample data

## Output

Always produce complete, valid OpenAPI 3.1 YAML that can be imported directly into tools like Swagger UI or Postman.
