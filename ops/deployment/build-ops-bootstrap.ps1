[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$')]
    [string]$Version
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$outputRoot = Join-Path $repositoryRoot "artifacts/ops-bootstrap/$Version"
$zipPath = Join-Path $repositoryRoot "artifacts/ops-bootstrap/thucluc-ops-bootstrap-$Version.zip"
if (Test-Path -LiteralPath $outputRoot) { throw "Output already exists: $outputRoot" }
if (Test-Path -LiteralPath $zipPath) { throw "Output already exists: $zipPath" }

New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
$image = "thucluc-ops-console:$Version"

Write-Host "Build Ops Console $image"
docker build --file (Join-Path $repositoryRoot 'ops/db-manager/Dockerfile') --tag $image (Join-Path $repositoryRoot 'ops/db-manager')
if ($LASTEXITCODE -ne 0) { throw 'Ops Console build failed.' }

docker save --output (Join-Path $outputRoot 'ops-console.tar') $image
if ($LASTEXITCODE -ne 0) { throw 'Ops Console image export failed.' }

Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'bootstrap/install.sh') -Destination $outputRoot
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'bootstrap/compose.yml') -Destination $outputRoot
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'bootstrap/environments.local.json') -Destination $outputRoot
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'infra/test-single-server/compose.yml') -Destination (Join-Path $outputRoot 'application.compose.yml')
$ascii = [System.Text.Encoding]::ASCII
[System.IO.File]::WriteAllText(
    (Join-Path $outputRoot 'ops-image.txt'),
    "$image`n",
    $ascii)

$checksumLines = foreach ($file in Get-ChildItem -LiteralPath $outputRoot -File | Sort-Object Name) {
    if ($file.Name -eq 'checksums.sha256') { continue }
    $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $($file.Name)"
}
[System.IO.File]::WriteAllText(
    (Join-Path $outputRoot 'checksums.sha256'),
    ($checksumLines -join "`n") + "`n",
    $ascii)
Compress-Archive -Path (Join-Path $outputRoot '*') -DestinationPath $zipPath -CompressionLevel Optimal

Write-Host "Completed: $zipPath"
