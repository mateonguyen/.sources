# DbManager regression checks

Run from the repository root:

```powershell
dotnet run --project ops/db-manager/tests/ThucLuc.DbManager.Checks/ThucLuc.DbManager.Checks.csproj
```

This package-free console harness exits with `0` when all checks pass and `1` when a check fails. It references the app and the ASP.NET Core shared framework, without adding a test framework or other NuGet packages.

The checks cover PDB input validation, generated SQL safety, pre-connection authorization guards, and static component rendering. They do not connect to Oracle, create a PDB, modify application configuration, or read saved credentials. Actual PDB provisioning, listener registration and Data Pump restore still require verification against a disposable Oracle database.
