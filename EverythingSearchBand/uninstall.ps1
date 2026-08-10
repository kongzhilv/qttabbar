#Requires -RunAsAdministrator
$ErrorActionPreference = 'SilentlyContinue'

$installDir = Join-Path $env:ProgramFiles 'EverythingSearchBand'
$dll = Join-Path $installDir 'EverythingSearchBand.dll'
$regasm = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v2.0.50727\RegAsm.exe'

Write-Host '[1/4] Hiding band in open Explorer windows...' -ForegroundColor Cyan
try {
    $shell = New-Object -ComObject Shell.Application
    foreach ($window in @($shell.Windows())) {
        try {
            $window.ShowBrowserBar('{B7C8D3D5-0C8E-42E9-BFE3-81DC8267AF61}', $false, $null)
        }
        catch {
        }
    }
}
catch {
}

Write-Host '[2/4] Unregistering COM components...' -ForegroundColor Cyan
if ((Test-Path -LiteralPath $regasm) -and (Test-Path -LiteralPath $dll)) {
    & $regasm $dll /nologo /unregister | Out-Null
}

Write-Host '[3/4] Removing files and settings...' -ForegroundColor Cyan
Remove-Item -LiteralPath $installDir -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item 'HKCU:\Software\EverythingSearchBand' -Recurse -Force -ErrorAction SilentlyContinue

Write-Host '[4/4] Restarting Explorer...' -ForegroundColor Cyan
Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2
Start-Process explorer.exe

Write-Host 'Everything Search Band removed.' -ForegroundColor Green
