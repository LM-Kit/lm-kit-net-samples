# REST API Conventions

## URL Structure

```
https://api.example.com/v1/resources/{id}/sub-resources
\_____________________/\_/\__________________________/
         |             |              |
       Host        Version        Path
```

## Naming Conventions

| Element | Convention | Example |
|---------|------------|---------|
| Resources | plural, kebab-case | `/user-accounts` |
| Path params | camelCase | `{userId}` |
| Query params | camelCase | `?sortBy=createdAt` |
| JSON fields | camelCase | `firstName` |

## Status Codes

### Success (2xx)
- `200 OK` - GET, PUT, PATCH success with body
- `201 Created` - POST success, include Location header
- `204 No Content` - DELETE success, no body

### Client Errors (4xx)
- `400 Bad Request` - Validation error
- `401 Unauthorized` - Missing/invalid auth
- `403 Forbidden` - Auth valid but not permitted
- `404 Not Found` - Resource doesn't exist
- `409 Conflict` - State conflict (duplicate, etc.)
- `422 Unprocessable Entity` - Semantic error
- `429 Too Many Requests` - Rate limited

### Server Errors (5xx)
- `500 Internal Server Error` - Unexpected error
- `502 Bad Gateway` - Upstream error
- `503 Service Unavailable` - Temporarily down

## Pagination

Use query parameters:
```
GET /users?page=2&limit=20
```

Response includes:
```json
{
  "data": [...],
  "pagination": {
    "page": 2,
    "limit": 20,
    "total": 156,
    "totalPages": 8
  }
}
```

## Filtering & Sorting

```
GET /products?category=electronics&minPrice=100&sortBy=price&order=desc
```

## Versioning

Use URL path versioning:
```
/v1/users
/v2/users
```

## Error Response Format

```json
{
  "code": "VALIDATION_ERROR",
  "message": "Request validation failed",
  "details": [
    {
      "field": "email",
      "message": "Invalid email format"
    }
  ],
  "requestId": "abc-123"
}
```
