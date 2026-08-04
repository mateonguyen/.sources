# Project Notes

## Database Engine

- Production-like and local integration database is Oracle.
- Do not assume SQL Server semantics when writing queries, migrations, or troubleshooting SQL exceptions.

## EF Core + Oracle Compatibility

- Prefer Oracle-safe LINQ patterns in hot paths.
- Avoid boolean-literal-sensitive query shapes in critical endpoints (especially AnyAsync/complex bool predicates) when provider compatibility is uncertain.
- For existence checks in Oracle-critical flows, prefer CountAsync(predicate) > 0.
- Keep Flyway as schema source of truth for all schema/index changes.
