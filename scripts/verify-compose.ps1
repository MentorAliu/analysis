[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$productionContainerName = "analysis-frontend-production-verification"
$productionImageName = "analysis-frontend:verification"
$productionContainerStarted = $false

if ([string]::IsNullOrWhiteSpace($env:POSTGRES_PASSWORD)) {
    $env:POSTGRES_PASSWORD = "compose-verification-$([Guid]::NewGuid().ToString('N'))"
}

function Invoke-Docker {
    param(
        [Parameter(Mandatory)]
        [string[]] $Arguments
    )

    & docker @Arguments

    if ($LASTEXITCODE -ne 0) {
        throw "docker $($Arguments -join ' ') failed with exit code $LASTEXITCODE"
    }
}

Push-Location $repositoryRoot

try {
    Invoke-Docker -Arguments @(
        "compose",
        "up",
        "--build",
        "--detach",
        "--wait",
        "--wait-timeout",
        "180"
    )

    $apiHealth = Invoke-RestMethod `
        -UseBasicParsing `
        -Uri "http://127.0.0.1:8080/health/ready"
    $proxyHealth = Invoke-RestMethod `
        -UseBasicParsing `
        -Uri "http://127.0.0.1:5173/api/health/ready"

    if ($apiHealth.status -ne "Healthy" -or $proxyHealth.status -ne "Healthy") {
        throw "API or Vite proxy readiness check did not return Healthy"
    }

    Invoke-Docker -Arguments @(
        "build",
        "--target",
        "production",
        "--tag",
        $productionImageName,
        "./frontend"
    )

    Invoke-Docker -Arguments @(
        "run",
        "--detach",
        "--rm",
        "--name",
        $productionContainerName,
        "--network",
        "crypto-analysis_default",
        "--publish",
        "127.0.0.1:5180:8080",
        $productionImageName
    )
    $productionContainerStarted = $true

    $deadline = (Get-Date).AddSeconds(90)

    do {
        Start-Sleep -Seconds 2
        $productionHealth = & docker inspect `
            $productionContainerName `
            --format "{{.State.Health.Status}}"
    } while (
        $productionHealth -ne "healthy" `
        -and (Get-Date) -lt $deadline
    )

    if ($productionHealth -ne "healthy") {
        throw "Production frontend did not become healthy"
    }

    $productionHealthResponse = Invoke-WebRequest `
        -UseBasicParsing `
        -Uri "http://127.0.0.1:5180/healthz"
    $productionProxyHealth = Invoke-RestMethod `
        -UseBasicParsing `
        -Uri "http://127.0.0.1:5180/api/health/ready"

    if (
        $productionHealthResponse.StatusCode -ne 200 `
        -or $productionProxyHealth.status -ne "Healthy"
    ) {
        throw "Production frontend health or API proxy check failed"
    }

    Write-Host "Compose and production frontend verification passed."
}
catch {
    & docker compose logs --no-color
    throw
}
finally {
    if ($productionContainerStarted) {
        & docker rm --force $productionContainerName | Out-Null
    }

    & docker compose down --volumes | Out-Null
    Pop-Location
}
