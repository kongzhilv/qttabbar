using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using BandObjectLib;
using Microsoft.Win32;

namespace EverythingSearchBand
{
    [ComVisible(true)]
    [Guid(BandGuid)]
    [ClassInterface(ClassInterfaceType.None)]
    public sealed class EverythingBand : BandObject
    {
        public const string BandGuid = "B7C8D3D5-0C8E-42E9-BFE3-81DC8267AF61";
        private const string DeskBandCategory = "{00021492-0000-0000-C000-000000000046}";
        private const int EmSetCueBanner = 0x1501;
        private const int BandHeight = 34;

        private readonly TextBox searchBox;
        private readonly Panel hostPanel;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        public EverythingBand()
        {
            hostPanel = new Panel();
            searchBox = new TextBox();

            SuspendLayout();
            hostPanel.SuspendLayout();

            hostPanel.Dock = DockStyle.Fill;
            hostPanel.Padding = new Padding(8, 5, 8, 5);

            searchBox.BorderStyle = BorderStyle.FixedSingle;
            searchBox.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            searchBox.Height = 24;
            searchBox.Width = 360;
            searchBox.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            searchBox.KeyDown += SearchBoxKeyDown;
            searchBox.HandleCreated += SearchBoxHandleCreated;

            hostPanel.Controls.Add(searchBox);
            Controls.Add(hostPanel);

            Height = BandHeight;
            MinSize = new Size(220, BandHeight);

            ApplyTheme();
            LayoutSearchBox();

            hostPanel.Resize += delegate { LayoutSearchBox(); };

            hostPanel.ResumeLayout(false);
            hostPanel.PerformLayout();
            ResumeLayout(false);
        }

        [ComRegisterFunction]
        public static void Register(Type type)
        {
            string clsid = type.GUID.ToString("B");
            using (RegistryKey key = Registry.ClassesRoot.CreateSubKey("CLSID\\" + clsid))
            {
                if (key != null)
                {
                    key.SetValue(null, "Everything Search Band");
                    key.SetValue("MenuText", "Everything Search");
                    key.SetValue("HelpText", "Search the current Explorer folder with Everything");
                }
            }

            using (RegistryKey category = Registry.ClassesRoot.CreateSubKey(
                "CLSID\\" + clsid + "\\Implemented Categories\\" + DeskBandCategory))
            {
            }
        }

        [ComUnregisterFunction]
        public static void Unregister(Type type)
        {
            string clsid = type.GUID.ToString("B");
            try
            {
                Registry.ClassesRoot.DeleteSubKeyTree(
                    "CLSID\\" + clsid + "\\Implemented Categories\\" + DeskBandCategory);
            }
            catch
            {
            }
        }

        protected override void OnExplorerAttached()
        {
            base.OnExplorerAttached();
            ApplyTheme();
            LayoutSearchBox();
        }

        public override void GetBandInfo(uint dwBandID, uint dwViewMode, ref DESKBANDINFO dbi)
        {
            base.GetBandInfo(dwBandID, dwViewMode, ref dbi);

            if ((dbi.dwMask & DBIM.ACTUAL) != 0)
            {
                dbi.ptActual.Y = BandHeight;
            }
            if ((dbi.dwMask & DBIM.MINSIZE) != 0)
            {
                dbi.ptMinSize.X = 220;
                dbi.ptMinSize.Y = BandHeight;
            }
            if ((dbi.dwMask & DBIM.MAXSIZE) != 0)
            {
                dbi.ptMaxSize.X = -1;
                dbi.ptMaxSize.Y = BandHeight;
            }
            if ((dbi.dwMask & DBIM.INTEGRAL) != 0)
            {
                dbi.ptIntegral.X = -1;
                dbi.ptIntegral.Y = 1;
            }
            if ((dbi.dwMask & DBIM.MODEFLAGS) != 0)
            {
                dbi.dwModeFlags = DBIMF.NORMAL | DBIMF.NOGRIPPER | DBIMF.NOMARGINS;
            }
            if ((dbi.dwMask & DBIM.TITLE) != 0)
            {
                dbi.wszTitle = null;
            }
            if ((dbi.dwMask & DBIM.BKCOLOR) != 0)
            {
                dbi.dwMask &= ~DBIM.BKCOLOR;
            }
        }

        private void SearchBoxHandleCreated(object sender, EventArgs e)
        {
            SendMessage(searchBox.Handle, EmSetCueBanner, (IntPtr)1, "Everything 搜索当前文件夹...");
        }

        private void SearchBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
            {
                return;
            }

            e.Handled = true;
            e.SuppressKeyPress = true;
            RunEverything(searchBox.Text);
        }

        private void LayoutSearchBox()
        {
            int availableWidth = Math.Max(200, hostPanel.ClientSize.Width - 16);
            int width = Math.Min(420, Math.Max(260, availableWidth / 2));
            searchBox.Width = width;
            searchBox.Left = Math.Max(8, hostPanel.ClientSize.Width - width - 8);
            searchBox.Top = Math.Max(4, (hostPanel.ClientSize.Height - searchBox.Height) / 2);
        }

        private void ApplyTheme()
        {
            bool dark = IsDarkMode();
            Color background = dark ? Color.FromArgb(32, 32, 32) : SystemColors.Control;
            Color inputBackground = dark ? Color.FromArgb(45, 45, 45) : SystemColors.Window;
            Color inputForeground = dark ? Color.White : SystemColors.WindowText;

            BackColor = background;
            hostPanel.BackColor = background;
            searchBox.BackColor = inputBackground;
            searchBox.ForeColor = inputForeground;
        }

        private static bool IsDarkMode()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    object value = key == null ? null : key.GetValue("AppsUseLightTheme");
                    if (value is int)
                    {
                        return (int)value == 0;
                    }
                }
            }
            catch
            {
            }
            return false;
        }

        private string GetCurrentFolder()
        {
            try
            {
                if (Explorer == null || string.IsNullOrEmpty(Explorer.LocationURL))
                {
                    return null;
                }

                Uri uri;
                if (Uri.TryCreate(Explorer.LocationURL, UriKind.Absolute, out uri) && uri.IsFile)
                {
                    return Uri.UnescapeDataString(uri.LocalPath);
                }
            }
            catch
            {
            }
            return null;
        }

        private void RunEverything(string query)
        {
            string currentFolder = GetCurrentFolder();
            if (string.IsNullOrEmpty(currentFolder))
            {
                MessageBox.Show(
                    "当前 Explorer 标签不是普通文件系统目录。",
                    "Everything Search",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            string everything = ResolveEverythingPath();
            if (string.IsNullOrEmpty(everything))
            {
                MessageBox.Show(
                    "未找到 Everything.exe。请安装 Everything，或把 Everything.exe 放在 D:\\Everything。",
                    "Everything Search",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            string args = "-nomaximized -path " + QuoteArgument(currentFolder) +
                          " -s " + QuoteArgument(query ?? string.Empty);

            ProcessStartInfo info = new ProcessStartInfo();
            info.FileName = everything;
            info.Arguments = args;
            info.UseShellExecute = true;
            Process.Start(info);
        }

        private static string ResolveEverythingPath()
        {
            string configured = ReadConfiguredEverythingPath();
            if (!string.IsNullOrEmpty(configured) && File.Exists(configured))
            {
                return configured;
            }

            string[] appNames = { "Everything64.exe", "Everything.exe" };
            RegistryKey[] roots = { Registry.CurrentUser, Registry.LocalMachine };
            foreach (RegistryKey root in roots)
            {
                foreach (string appName in appNames)
                {
                    try
                    {
                        using (RegistryKey key = root.OpenSubKey(
                            @"Software\Microsoft\Windows\CurrentVersion\App Paths\" + appName))
                        {
                            string path = key == null ? null : key.GetValue(null) as string;
                            if (!string.IsNullOrEmpty(path) && File.Exists(path))
                            {
                                return path;
                            }
                        }
                    }
                    catch
                    {
                    }
                }
            }

            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string programFilesX86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)");
            string[] candidates =
            {
                @"D:\Everything\Everything.exe",
                @"D:\Everything\Everything64.exe",
                Path.Combine(Path.Combine(programFiles, "Everything"), "Everything.exe"),
                Path.Combine(Path.Combine(programFiles, "Everything"), "Everything64.exe"),
                Path.Combine(Path.Combine(programFiles, "Everything 1.5a"), "Everything.exe"),
                Path.Combine(Path.Combine(programFiles, "Everything 1.5a"), "Everything64.exe")
            };

            foreach (string candidate in candidates)
            {
                if (!string.IsNullOrEmpty(candidate) && File.Exists(candidate))
                {
                    return candidate;
                }
            }

            if (!string.IsNullOrEmpty(programFilesX86))
            {
                string x86 = Path.Combine(Path.Combine(programFilesX86, "Everything"), "Everything.exe");
                if (File.Exists(x86))
                {
                    return x86;
                }
            }

            return null;
        }

        private static string ReadConfiguredEverythingPath()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\EverythingSearchBand"))
                {
                    return key == null ? null : key.GetValue("EverythingPath") as string;
                }
            }
            catch
            {
                return null;
            }
        }

        private static string QuoteArgument(string value)
        {
            if (value == null)
            {
                return "\"\"";
            }

            StringBuilder builder = new StringBuilder();
            builder.Append('"');
            int slashes = 0;
            foreach (char c in value)
            {
                if (c == '\\')
                {
                    slashes++;
                    continue;
                }

                if (c == '"')
                {
                    builder.Append('\\', slashes * 2 + 1);
                    builder.Append('"');
                    slashes = 0;
                    continue;
                }

                if (slashes > 0)
                {
                    builder.Append('\\', slashes);
                    slashes = 0;
                }
                builder.Append(c);
            }

            if (slashes > 0)
            {
                builder.Append('\\', slashes * 2);
            }
            builder.Append('"');
            return builder.ToString();
        }
    }

    [ComVisible(true)]
    [Guid("8357BA9D-4D37-4FE8-916F-8D3BD979FE55")]
    [ClassInterface(ClassInterfaceType.None)]
    public sealed class EverythingBandAutoLoader : IObjectWithSite
    {
        private const string BhoKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Browser Helper Objects\";
        private object site;

        [ComRegisterFunction]
        public static void Register(Type type)
        {
            string clsid = type.GUID.ToString("B");
            using (RegistryKey key = Registry.ClassesRoot.CreateSubKey("CLSID\\" + clsid))
            {
                if (key != null)
                {
                    key.SetValue(null, "Everything Search Band AutoLoader");
                }
            }
            using (RegistryKey key = Registry.LocalMachine.CreateSubKey(BhoKey + clsid))
            {
            }
        }

        [ComUnregisterFunction]
        public static void Unregister(Type type)
        {
            try
            {
                using (RegistryKey root = Registry.LocalMachine.CreateSubKey(BhoKey))
                {
                    if (root != null)
                    {
                        root.DeleteSubKey(type.GUID.ToString("B"), false);
                    }
                }
            }
            catch
            {
            }
        }

        public int SetSite(object newSite)
        {
            site = newSite;
            if (newSite == null || !string.Equals(Process.GetCurrentProcess().ProcessName, "explorer", StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            try
            {
                object guid = new Guid(EverythingBand.BandGuid).ToString("B");
                object show = true;
                object size = null;
                newSite.GetType().InvokeMember(
                    "ShowBrowserBar",
                    BindingFlags.InvokeMethod,
                    null,
                    newSite,
                    new object[] { guid, show, size });
            }
            catch
            {
            }
            return 0;
        }

        public int GetSite(ref Guid riid, out object ppvSite)
        {
            ppvSite = site;
            return 0;
        }
    }
}
