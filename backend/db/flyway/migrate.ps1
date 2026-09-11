param(
    [string]$DbHost = "host.docker.internal",
    [int]$DbPort = 1521,
    [string]$ServiceName = "XEPDB1",
    [string]$DbSchema = "CAND_QLCNTT",
    [string]$DbUser = "CAND_QLCNTT",
    [string]$DbPassword = "123456",
    [string]$FlywayImage = "flyway/flyway:10-alpine",
    [ValidateSet("migrate", "repair")]
    [string]$MigrationAction = "migrate"
)

$ErrorActionPreference = "Stop"

$sqlDir = Join-Path $PSScriptRoot "sql"
if (-not (Test-Path $sqlDir)) {
    throw "Flyway SQL directory not found: $sqlDir"
}

$jdbcUrl = "jdbc:oracle:thin:@//$DbHost`:$DbPort/$ServiceName"

Write-Host "Running Flyway $MigrationAction against $jdbcUrl (schema=$DbSchema)..." -ForegroundColor Cyan

docker run --rm `
    -v "${sqlDir}:/flyway/sql" `
    $FlywayImage `
    "-url=$jdbcUrl" `
    "-user=$DbUser" `
    "-password=$DbPassword" `
    "-schemas=$DbSchema" `
    $MigrationAction

if ($LASTEXITCODE -ne 0) {
    throw "Flyway $MigrationAction failed with exit code $LASTEXITCODE"
}

Write-Host "Flyway $MigrationAction completed successfully." -ForegroundColor Green
