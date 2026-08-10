$ErrorActionPreference = 'Stop'

$path = 'InstallerMini/Installer.wxs'
$text = [IO.File]::ReadAllText($path)

$replacements = @(
    @('<\?define ProductVersion="1\.5\.6\.1" \?>', '<?define ProductVersion="1.5.6.2" ?>'),
    @('<\?define VersionString="1\.5\.6\.1 Beta\(2023\)" \?>', '<?define VersionString="1.5.6.2 Native Tabs + Everything (2026)" ?>'),
    @('<\?define StrongName="QTTabBar, Version=1\.5\.6\.1, Culture=neutral, PublicKeyToken=973461f1cd23d8eb"\?>', '<?define StrongName="QTTabBar, Version=1.5.6.2, Culture=neutral, PublicKeyToken=973461f1cd23d8eb"?>'),
    @('<PropertyRef Id="NETFRAMEWORK35"/>\s*<PropertyRef Id="NETFRAMEWORK40CLIENT"/>', '<PropertyRef Id="WIX_IS_NETFRAMEWORK_48_OR_LATER_INSTALLED"/>'),
    @('https://github\.com/indiff/qttabbar/releases', 'https://github.com/kongzhilv/qttabbar/releases'),
    @('https://github\.com/indiff/qttabbar', 'https://github.com/kongzhilv/qttabbar'),
    @('https://indiff\.github\.io/qttabbar/', 'https://github.com/kongzhilv/qttabbar'),
    @('Value="v2\.0\.50727"', 'Value="v4.0.30319"'),
    @('<!\[CDATA\[Installed OR NETFRAMEWORK35 OR NETFRAMEWORK40CLIENT\]\]>', '<![CDATA[Installed OR WIX_IS_NETFRAMEWORK_48_OR_LATER_INSTALLED]]>')
)

foreach ($pair in $replacements) {
    $pattern = $pair[0]
    $replacement = $pair[1]
    if (-not [regex]::IsMatch($text, $pattern)) {
        throw "Required installer pattern was not found: $pattern"
    }
    $text = [regex]::Replace($text, $pattern, $replacement)
}

$text = [regex]::Replace($text, '^\s*<CustomAction Id="SetRuntime4".*?\r?\n', '', [Text.RegularExpressions.RegexOptions]::Multiline)
$text = [regex]::Replace($text, '^\s*<CustomAction Id="SetRuntimeNot4".*?\r?\n', '', [Text.RegularExpressions.RegexOptions]::Multiline)
$text = [regex]::Replace($text, '^\s*<Custom Action="SetRuntime4".*?\r?\n', '', [Text.RegularExpressions.RegexOptions]::Multiline)
$text = [regex]::Replace($text, '^\s*<Custom Action="SetRuntimeNot4".*?\r?\n', '', [Text.RegularExpressions.RegexOptions]::Multiline)

[IO.File]::WriteAllText($path, $text, (New-Object Text.UTF8Encoding($true)))
Write-Host 'InstallerMini metadata updated for QTTabBar 1.5.6.2 / CLR4.8.'
