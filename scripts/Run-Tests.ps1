
param(
    [string]$DatabaseHost = "127.0.0.1",
    [int]$Port = 5432,
    [string]$Username = "postgres"
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
$solutionPath = Join-Path $projectRoot "StockGuard.sln"

$previousConnection = $env:STOCKGUARD_TEST_CONNECTION_STRING
$exitCode = 1

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "       STOCKGUARD AUDIT TEST RUNNER" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

try {
    if (-not (Test-Path $solutionPath)) {
        throw "StockGuard.sln could not be found."
    }

    $credential = Get-Credential `
        -UserName $Username `
        -Message "Enter your PostgreSQL test database password"

    if ($null -eq $credential) {
        throw "Database credential entry was cancelled."
    }

    $builder = New-Object System.Data.Common.DbConnectionStringBuilder

    $builder["Host"] = $DatabaseHost
    $builder["Port"] = $Port
    $builder["Database"] = "stockguard_test"
    $builder["Username"] = $credential.UserName
    $builder["Password"] = $credential.GetNetworkCredential().Password

    $env:STOCKGUARD_TEST_CONNECTION_STRING = $builder.ConnectionString

    Remove-Variable credential, builder

    Write-Host "Test database: stockguard_test" -ForegroundColor Green
    Write-Host "Running automated tests..." -ForegroundColor Cyan
    Write-Host ""

    dotnet test $solutionPath

    $exitCode = $LASTEXITCODE

    if ($exitCode -eq 0) {
        Write-Host ""
        Write-Host "ALL TESTS PASSED" -ForegroundColor Green
    }
    else {
        Write-Host ""
        Write-Host "TESTS FAILED" -ForegroundColor Red
    }
}
catch {
    Write-Host $_.Exception.Message -ForegroundColor Red
    $exitCode = 1
}
finally {
    if ($null -eq $previousConnection) {
        Remove-Item Env:STOCKGUARD_TEST_CONNECTION_STRING `
            -ErrorAction SilentlyContinue
    }
    else {
        $env:STOCKGUARD_TEST_CONNECTION_STRING = $previousConnection
    }

    Remove-Variable credential, builder, previousConnection `
        -ErrorAction SilentlyContinue

    Write-Host ""
    Write-Host "Previous environment restored." -ForegroundColor Cyan
}

exit $exitCode
