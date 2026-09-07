#Requires -Version 7.0
[CmdletBinding()]
param(
    # Use already-built local images when source has not changed.
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$repo = Split-Path -Parent $PSScriptRoot
$envFile = Join-Path $repo '.env'
$compose = @('compose', '-p', 'analysis-local', '--env-file', $envFile,
    '-f', (Join-Path $repo 'compose.yaml'))

function Invoke-LocalDocker {
    & docker @args
    if ($LASTEXITCODE -ne 0) {
        throw "Local startup stopped: Docker exited with code $LASTEXITCODE. Resolve the error and run this script again."
    }
}

if (!(Test-Path -LiteralPath $envFile)) {
    $nodeImage = 'node:24.20.0-bookworm-slim@sha256:ba849c60be29959425b8734d57b8b4b7d56f98edd9504c9af091d5281095a71e'
    Invoke-LocalDocker run --rm --mount "type=bind,source=$repo,target=/workspace" `
        --workdir /workspace $nodeImage node scripts/init-local.mjs
}

Invoke-LocalDocker @compose config --quiet
if (!$SkipBuild) {
    # Build before stopping an already-running app; a build failure leaves it available.
    Invoke-LocalDocker @compose build frontend api worker
}

# Keep readers and ordinary workers stopped until the maintenance command succeeds.
Invoke-LocalDocker @compose stop frontend api worker
Invoke-LocalDocker @compose up --detach --no-build --wait --wait-timeout 120 postgres redis
Invoke-LocalDocker @compose run --rm --no-deps --pull never -T worker --migrate
Invoke-LocalDocker @compose up --detach --no-build --wait --wait-timeout 120
Invoke-LocalDocker @compose ps
Write-Host 'Local app ready. Use the frontend address shown above. No provider data was acquired.'
