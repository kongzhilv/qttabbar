$ErrorActionPreference = 'Stop'

function Replace-RegexRequired {
    param(
        [string]$Text,
        [string]$Pattern,
        [string]$Replacement,
        [string]$Label
    )

    $matches = [regex]::Matches($Text, $Pattern, [Text.RegularExpressions.RegexOptions]::Singleline)
    if ($matches.Count -ne 1) {
        throw "Expected exactly one match for $Label, found $($matches.Count)."
    }

    return [regex]::Replace(
        $Text,
        $Pattern,
        $Replacement,
        [Text.RegularExpressions.RegexOptions]::Singleline
    )
}

$utf8Bom = New-Object System.Text.UTF8Encoding($true)

# 1. Integrate Everything search into the real QTButtonBar implementation.
$buttonPath = 'QTTabBar/QTButtonBar.cs'
$button = [IO.File]::ReadAllText($buttonPath)

$button = Replace-RegexRequired $button 'Height = BarHeight \+ 100\s*;' 'Height = BarHeight;' 'QTButtonBar initial height'
$button = Replace-RegexRequired $button 'MinSize = new Size\(20, BarHeight \+ 100\);' 'MinSize = new Size(20, BarHeight);' 'QTButtonBar minimum height'

$button = Replace-RegexRequired $button 'foreach\(int index in Config\.BBar\.ButtonIndexes\) \{' @'
int[] buttonIndexes = QTUtility.IsThanWin11
                    ? new int[] { BII_FILTERBAR }
                    : Config.BBar.ButtonIndexes;
            foreach(int index in buttonIndexes) {
'@ 'Windows 11 core toolbar composition'

$button = Replace-RegexRequired $button 'ShellViewIncrementalSearch\(text\);\s*e\.Handled = true;' @'
LaunchEverythingSearch(text);
                    e.Handled = true;
'@ 'Enter key Everything search'

$button = Replace-RegexRequired $button '        private void searchBox_TextChanged\(object sender, EventArgs e\) \{.*?        \}\r?\n\r?\n        private static int SearchBoxWidth' @'
        private void searchBox_TextChanged(object sender, EventArgs e) {
            timerSerachBox_Search.Stop();
            timerSearchBox_Rearrange.Stop();
            strSearch = searchBox.Text;
            fSearchBoxInputStart = false;
            iSearchResultCount = -1;
        }

        private bool LaunchEverythingSearch(string query) {
            if(String.IsNullOrEmpty(query)) return false;

            try {
                string executable = ResolveEverythingPath();
                if(String.IsNullOrEmpty(executable)) {
                    MessageBox.Show(
                        "Everything.exe was not found. Set HKCU\\Software\\QTTabBar\\EverythingPath to your Everything.exe path.",
                        "QTTabBar Everything Search",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return false;
                }

                string folder = GetCurrentExplorerPath();
                StringBuilder arguments = new StringBuilder("-nonewwindow -nomaximized ");
                if(!String.IsNullOrEmpty(folder)) {
                    arguments.Append("-path ").Append(QuoteProcessArgument(folder)).Append(' ');
                }
                arguments.Append("-s ").Append(QuoteProcessArgument(query));

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo {
                    FileName = executable,
                    Arguments = arguments.ToString(),
                    UseShellExecute = true
                });
                return true;
            }
            catch(Exception exception) {
                QTUtility2.MakeErrorLog(exception, "QTButtonBar Everything search");
                MessageBox.Show(
                    exception.Message,
                    "QTTabBar Everything Search",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return false;
            }
        }

        private string GetCurrentExplorerPath() {
            if(Explorer == null) return null;

            string locationUrl = Explorer.LocationURL;
            if(String.IsNullOrEmpty(locationUrl)) return null;

            Uri uri;
            if(Uri.TryCreate(locationUrl, UriKind.Absolute, out uri) && uri.IsFile) {
                return uri.LocalPath;
            }
            return null;
        }

        private static string ResolveEverythingPath() {
            string configured = null;
            using(RegistryKey key = Registry.CurrentUser.OpenSubKey(RegConst.Root)) {
                if(key != null) configured = key.GetValue("EverythingPath", null) as string;
            }

            string programFiles = Environment.GetEnvironmentVariable("ProgramW6432")
                    ?? Environment.GetEnvironmentVariable("ProgramFiles");
            string programFilesX86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)");

            string[] candidates = new string[] {
                configured,
                @"D:\Everything\Everything.exe",
                @"D:\Everything\Everything64.exe",
                String.IsNullOrEmpty(programFiles) ? null : Path.Combine(programFiles, @"Everything\Everything.exe"),
                String.IsNullOrEmpty(programFilesX86) ? null : Path.Combine(programFilesX86, @"Everything\Everything.exe")
            };

            foreach(string candidate in candidates) {
                if(!String.IsNullOrEmpty(candidate) && File.Exists(candidate)) return candidate;
            }
            return null;
        }

        private static string QuoteProcessArgument(string value) {
            return "\"" + (value ?? String.Empty).Replace("\"", "\\\"") + "\"";
        }

        private static int SearchBoxWidth
'@ 'Replace incremental filter with Everything launcher'

[IO.File]::WriteAllText($buttonPath, $button, $utf8Bom)

# 2. Keep the real QTTabBar COM object loaded as Explorer's backend on Windows 11,
#    while collapsing only QTTabBar's legacy visual tab strip so native Explorer tabs remain visible.
$tabPath = 'QTTabBar/QTTabBarClass.cs'
$tab = [IO.File]::ReadAllText($tabPath)

$tab = Replace-RegexRequired $tab 'BandHeight = Config\.Skin\.TabHeight \+ BandHeightSpace;\s*// BandHeight = Config\.Skin\.TabHeight \+ 10;\s*InitializeComponent\(\);' @'
BandHeight = QTUtility.IsThanWin11 ? 1 : Config.Skin.TabHeight + BandHeightSpace;
            // BandHeight = Config.Skin.TabHeight + 10;
            InitializeComponent();
            if(QTUtility.IsThanWin11) {
                if(tabControl1 != null) tabControl1.Visible = false;
                Height = 1;
                MinSize = new Size(1, 1);
            }
'@ 'Collapse legacy QTTabBar strip on Windows 11'

[IO.File]::WriteAllText($tabPath, $tab, $utf8Bom)

Write-Host 'Core QTTabBar native-tabs + Everything integration patch applied.'
