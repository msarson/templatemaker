<#
    Builds the Clarion Template Tools installer.

      1. publishes the WPF designer self-contained (win-x64) into payload\app
      2. runs Inno Setup (ISCC) on ClarionTemplateTools.iss
      3. leaves ClarionTemplateToolsSetup.exe in installer\Output

    Usage:   pwsh installer\build-installer.ps1
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$Runtime       = 'win-x64'
)

$ErrorActionPreference = 'Stop'
$here    = $PSScriptRoot
$repo    = Split-Path $here -Parent
$proj    = Join-Path $repo 'designer\ClarionTplDesigner\ClarionTplDesigner.csproj'
$payload = Join-Path $here 'payload'
$appOut  = Join-Path $payload 'app'
$iss     = Join-Path $here 'ClarionTemplateTools.iss'

# Locate the Inno Setup compiler.
$iscc = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1

if (-not $iscc) {
    throw "Inno Setup 6 (ISCC.exe) not found. Install it from https://jrsoftware.org/isdl.php"
}

Write-Host "==> Cleaning payload" -ForegroundColor Cyan
if (Test-Path $payload) { Remove-Item $payload -Recurse -Force }

Write-Host "==> Publishing designer ($Configuration / $Runtime, self-contained)" -ForegroundColor Cyan
dotnet publish $proj -c $Configuration -r $Runtime --self-contained true `
    -p:PublishSingleFile=false -p:DebugType=none -o $appOut
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed ($LASTEXITCODE)" }

Write-Host "==> Compiling installer with Inno Setup" -ForegroundColor Cyan
& $iscc $iss
if ($LASTEXITCODE -ne 0) { throw "ISCC failed ($LASTEXITCODE)" }

$setup = Join-Path $here 'Output\ClarionTemplateToolsSetup.exe'
Write-Host ""
Write-Host "==> Done: $setup" -ForegroundColor Green
if (Test-Path $setup) {
    "{0:N1} MB" -f ((Get-Item $setup).Length / 1MB) | Write-Host
}
