# ThucLuc Backend Runbook

## Prerequisites

- .NET SDK 8.x
- Oracle database schema available
- Optional: MinIO and Gotenberg for file/PDF integration

## Local setup

1. Copy `src/Api/appsettings.Example.json` into `src/Api/appsettings.Development.json` and update secrets.
2. Ensure connection string points to your schema.
3. Optional seed: set `Seed:ApplyOnStartup=true` for first run.

## Run

```bash
dotnet restore ThucLuc.sln
dotnet build ThucLuc.sln
dotnet run --project src/Api/ThucLuc.Api.csproj
```

## Test

```bash
dotnet test ThucLuc.sln
```

## Flyway

- SQL migration files are in `db/flyway/sql`.
- Apply in order for new environments.

## CI quality gate

- GitHub Actions workflow `.github/workflows/ci.yml` validates:
  - backend restore/build with warnings as errors
  - backend tests
  - frontend build
