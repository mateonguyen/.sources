[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$')]
    [string]$Version,

    [string]$CommitSha = 'unknown',

    [ValidateSet('19', '21')]
    [string]$MinimumOracleVersion = '19',

    [ValidateSet('Backend', 'Frontend', 'OpsConsole')]
    [string[]]$Components = @('Backend', 'Frontend'),

    [string]$GotenbergSourceImage = '',

    [string]$MinioSourceImage = '',

    [string]$FlywaySourceImage = 'flyway/flyway:10-alpine'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$releaseRoot = Join-Path $repositoryRoot "artifacts/releases/$Version"
$zipPath = Join-Path $repositoryRoot "artifacts/releases/thucluc-release-$Version.zip"

if (Test-Path -LiteralPath $releaseRoot) {
    throw "Release directory already exists: $releaseRoot. Use a new version or remove the old release after checking it."
}

if (Test-Path -LiteralPath $zipPath) {
    throw "Release file already exists: $zipPath. Use a new version or remove the old release after checking it."
}

New-Item -ItemType Directory -Path $releaseRoot -Force | Out-Null

$buildBackend = $Components -contains 'Backend'
$buildFrontend = $Components -contains 'Frontend'
$buildOpsConsole = $Components -contains 'OpsConsole'
if (-not $buildBackend -and -not $buildFrontend -and -not $buildOpsConsole `
    -and [string]::IsNullOrWhiteSpace($GotenbergSourceImage) `
    -and [string]::IsNullOrWhiteSpace($MinioSourceImage)) {
    throw 'Select at least one component.'
}

$backendImage = if ($buildBackend) { "thucluc-backend:$Version" } else { '' }
$frontendImage = if ($buildFrontend) { "thucluc-frontend:$Version" } else { '' }
$opsImage = if ($buildOpsConsole) { "thucluc-ops-console:$Version" } else { '' }
$flywayImage = if ($buildBackend) { "thucluc-flyway:$Version" } else { '' }

if ($buildBackend) {
    Write-Host "Build backend $backendImage"
    docker build --file (Join-Path $repositoryRoot 'backend/Dockerfile') --tag $backendImage (Join-Path $repositoryRoot 'backend')
    if ($LASTEXITCODE -ne 0) { throw 'Backend build failed.' }
}

if ($buildFrontend) {
    Write-Host "Build frontend $frontendImage"
    docker build --file (Join-Path $repositoryRoot 'frontend/Dockerfile') --tag $frontendImage (Join-Path $repositoryRoot 'frontend')
    if ($LASTEXITCODE -ne 0) { throw 'Frontend build failed.' }
}

if ($buildOpsConsole) {
    Write-Host "Build Ops Console $opsImage"
    docker build --file (Join-Path $repositoryRoot 'ops/db-manager/Dockerfile') --tag $opsImage (Join-Path $repositoryRoot 'ops/db-manager')
    if ($LASTEXITCODE -ne 0) { throw 'Ops Console build failed.' }
}

if ($buildBackend) {
    docker image inspect $FlywaySourceImage | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Flyway source image is not available: $FlywaySourceImage" }
    docker tag $FlywaySourceImage $flywayImage
    if ($LASTEXITCODE -ne 0) { throw 'Flyway image tag failed.' }
}

if ($buildBackend) {
    docker save --output (Join-Path $releaseRoot "backend-$Version.tar") $backendImage
    if ($LASTEXITCODE -ne 0) { throw 'Backend image export failed.' }
}

if ($buildFrontend) {
    docker save --output (Join-Path $releaseRoot "frontend-$Version.tar") $frontendImage
    if ($LASTEXITCODE -ne 0) { throw 'Frontend image export failed.' }
}

if ($buildOpsConsole) {
    docker save --output (Join-Path $releaseRoot "ops-console-$Version.tar") $opsImage
    if ($LASTEXITCODE -ne 0) { throw 'Ops Console image export failed.' }
}

if ($buildBackend) {
    docker save --output (Join-Path $releaseRoot "flyway-$Version.tar") $flywayImage
    if ($LASTEXITCODE -ne 0) { throw 'Flyway image export failed.' }
}

$gotenbergImage = ''
if (-not [string]::IsNullOrWhiteSpace($GotenbergSourceImage)) {
    $gotenbergImage = "thucluc-gotenberg:$Version"
    docker image inspect $GotenbergSourceImage | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Gotenberg source image is not available: $GotenbergSourceImage" }
    docker tag $GotenbergSourceImage $gotenbergImage
    if ($LASTEXITCODE -ne 0) { throw 'Gotenberg image tag failed.' }
    docker save --output (Join-Path $releaseRoot "gotenberg-$Version.tar") $gotenbergImage
    if ($LASTEXITCODE -ne 0) { throw 'Gotenberg image export failed.' }
}

$minioImage = ''
if (-not [string]::IsNullOrWhiteSpace($MinioSourceImage)) {
    $minioImage = "thucluc-minio:$Version"
    docker image inspect $MinioSourceImage | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "MinIO source image is not available: $MinioSourceImage" }
    docker tag $MinioSourceImage $minioImage
    if ($LASTEXITCODE -ne 0) { throw 'MinIO image tag failed.' }
    docker save --output (Join-Path $releaseRoot "minio-$Version.tar") $minioImage
    if ($LASTEXITCODE -ne 0) { throw 'MinIO image export failed.' }
}

[string[]]$migrationFiles = @()
if ($buildBackend) {
    $flywaySource = Join-Path $repositoryRoot 'backend/db/flyway/sql'
    $flywayDestination = Join-Path $releaseRoot 'flyway/sql'
    New-Item -ItemType Directory -Path $flywayDestination -Force | Out-Null
    Copy-Item -LiteralPath (Get-ChildItem -LiteralPath $flywaySource -File).FullName -Destination $flywayDestination
    $migrationFiles = @(Get-ChildItem -LiteralPath $flywayDestination -File | Select-Object -ExpandProperty Name)
    [Array]::Sort($migrationFiles, [StringComparer]::OrdinalIgnoreCase)
}
$manifest = [ordered]@{
    version = $Version
    backendImage = $backendImage
    frontendImage = $frontendImage
    opsConsoleImage = $opsImage
    flywayImage = $flywayImage
    gotenbergImage = $gotenbergImage
    minioImage = $minioImage
    minimumOracleVersion = $MinimumOracleVersion
    commitSha = $CommitSha
    createdAt = [DateTimeOffset]::Now.ToString('o')
    migrations = $migrationFiles
}
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $releaseRoot 'release-manifest.json') -Encoding utf8

$checksumLines = foreach ($file in Get-ChildItem -LiteralPath $releaseRoot -Recurse -File | Sort-Object FullName) {
    if ($file.Name -eq 'checksums.sha256') { continue }
    $relativePath = $file.FullName.Substring($releaseRoot.Length + 1).Replace('\', '/')
    $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $relativePath"
}
[System.IO.File]::WriteAllText(
    (Join-Path $releaseRoot 'checksums.sha256'),
    ($checksumLines -join "`n") + "`n",
    [System.Text.Encoding]::ASCII)

# Compress-Archive on Windows stores nested entry names with backslashes. Those
# names are treated as literal characters when the package is extracted on
# Linux, so paths such as flyway/sql/... no longer match checksums.sha256.
# Create entries explicitly with portable ZIP separators instead.
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::Open(
    $zipPath,
    [System.IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($file in Get-ChildItem -LiteralPath $releaseRoot -Recurse -File | Sort-Object FullName) {
        $entryName = $file.FullName.Substring($releaseRoot.Length + 1).Replace('\', '/')
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $archive,
            $file.FullName,
            $entryName,
            [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
}
finally {
    $archive.Dispose()
}

Write-Host "Completed: $zipPath"
