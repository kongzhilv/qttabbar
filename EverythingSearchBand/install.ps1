#Requires -RunAsAdministrator
$ErrorActionPreference = 'Stop'

$installDir = Join-Path $env:ProgramFiles 'EverythingSearchBand'
$sourceDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$dll = Join-Path $installDir 'EverythingSearchBand.dll'
$regasm = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v2.0.50727\RegAsm.exe'

Write-Host '[1/5] Installing files...' -ForegroundColor Cyan
New-Item -ItemType Directory -Path $installDir -Force | Out-Null
Get-ChildItem -LiteralPath $sourceDir -File | Where-Object {
    $_.Extension -in '.dll', '.pdb'
} | Copy-Item -Destination $installDir -Force

if (-not (Test-Path -LiteralPath $dll)) {
    throw "Missing $dll"
}
if (-not (Test-Path -LiteralPath $regasm)) {
    throw "Missing CLR2 RegAsm: $regasm"
}

Write-Host '[2/5] Registering COM deskband and autoloader...' -ForegroundColor Cyan
& $regasm $dll /nologo /codebase
if ($LASTEXITCODE -ne 0) {
    throw "RegAsm failed with exit code $LASTEXITCODE"
}

Write-Host '[3/5] Saving optional Everything path...' -ForegroundColor Cyan
$knownEverything = @(
    'D:\Everything\Everything.exe',
    'D:\Everything\Everything64.exe'
) | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1

if ($knownEverything) {
    $key = 'HKCU:\Software\EverythingSearchBand'
    New-Item -Path $key -Force | Out-Null
    New-ItemProperty -Path $key -Name EverythingPath -PropertyType String -Value $knownEverything -Force | Out-Null
    Write-Host "Everything: $knownEverything" -ForegroundColor Green
}

Write-Host '[4/5] Restarting Explorer...' -ForegroundColor Cyan
Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2
Start-Process explorer.exe
Start-Sleep -Seconds 3

Write-Host '[5/5] Asking open Explorer windows to show the band...' -ForegroundColor Cyan
try {
    $shell = New-Object -ComObject Shell.Application
    foreach ($window in @($shell.Windows())) {
        try {
            $window.ShowBrowserBar('{B7C8D3D5-0C8E-42E9-BFE3-81DC8267AF61}', $true, $null)
        }
        catch {
        }
    }
}
catch {
}

Write-Host ''
Write-Host 'Everything Search Band installed.' -ForegroundColor Green
Write-Host 'Keep Windows 11 native tabs enabled. The band is independent of QTTabBar tabs.' -ForegroundColor Green
