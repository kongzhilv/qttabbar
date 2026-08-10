# Everything Search Band

A standalone Explorer deskband that keeps the Windows 11 native tab bar and adds a compact Everything-powered search field.

## Goals

- Keep Windows 11 native tabs.
- Do not require the QTTabBar tab bar to be visible.
- Search the current Explorer folder with Everything.
- Avoid the extra `+100` toolbar height present in the fork's QTButtonBar implementation.
- Match light/dark Explorer themes reasonably closely.

## Requirements

- Windows 11 x64.
- .NET Framework 3.5 enabled.
- Everything installed or available as `D:\Everything\Everything.exe` / `Everything64.exe`.
- Run installation from an elevated PowerShell window.

## Install

Extract the release ZIP and run:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\install.ps1
```

The installer registers the deskband and its Explorer auto-loader, restarts Explorer, and asks open Explorer windows to show the band.

## Use

Type a query in the search box and press Enter. The band launches Everything with the current Explorer folder as the search path.

The command line shape is:

```text
Everything.exe -nomaximized -path "CURRENT_FOLDER" -s "QUERY"
```

## Optional custom Everything path

Set:

```powershell
New-Item 'HKCU:\Software\EverythingSearchBand' -Force | Out-Null
New-ItemProperty 'HKCU:\Software\EverythingSearchBand' -Name EverythingPath -PropertyType String -Value 'D:\YourPath\Everything.exe' -Force
```

## Uninstall

Run elevated PowerShell:

```powershell
.\uninstall.ps1
```

## Architecture

This is a separate self-contained COM deskband assembly. It includes only the minimal Explorer deskband/COM compatibility layer it needs and has no runtime dependency on `QTTabBarClass`, `PluginServer`, the QTTabBar tab strip, or a separate `BandObjectLib.dll`. The included Browser Helper Object auto-loader calls `ShowBrowserBar` for the Everything band on Explorer windows.
