param(
    [string]$DbHost = "host.docker.internal",
    [int]$DbPort = 1521,
    [string]$ServiceName = "XEPDB1",
    [string]$DbSchema = "CAND_QLCNTT",
    [string]$DbUser = "CAND_QLCNTT",
    [string]$DbPassword = "123456",
    [string]$FlywayImage = "flyway/flyway:10-alpine"
)

$ErrorActionPreference = "Stop"

$sqlDir = Join-Path $PSScriptRoot "sql"
if (-not (Test-Path $sqlDir)) {
    throw "Flyway SQL directory not found: $sqlDir"
}

$jdbcUrl = "jdbc:oracle:thin:@//$DbHost`:$DbPort/$ServiceName"

Write-Host "Running Flyway repair against $jdbcUrl (schema=$DbSchema)..." -ForegroundColor Cyan

docker run --rm `
    -v "${sqlDir}:/flyway/sql" `
    $FlywayImage `
    "-url=$jdbcUrl" `
    "-user=$DbUser" `
    "-password=$DbPassword" `
    "-schemas=$DbSchema" `
    repair

if ($LASTEXITCODE -ne 0) {
    throw "Flyway repair failed with exit code $LASTEXITCODE"
}

Write-Host "Flyway repair completed successfully." -ForegroundColor Green