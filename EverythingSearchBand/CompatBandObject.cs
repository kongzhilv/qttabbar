using System;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
using System.Windows.Forms;

namespace BandObjectLib
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct DESKBANDINFO
    {
        public uint dwMask;
        public POINTL ptMinSize;
        public POINTL ptMaxSize;
        public POINTL ptIntegral;
        public POINTL ptActual;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string wszTitle;
        public DBIMF dwModeFlags;
        public int crBkgnd;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINTL
    {
        public int X;
        public int Y;
    }

    [Flags]
    public enum DBIM : uint
    {
        MINSIZE = 0x0001,
        MAXSIZE = 0x0002,
        INTEGRAL = 0x0004,
        ACTUAL = 0x0008,
        TITLE = 0x0010,
        MODEFLAGS = 0x0020,
        BKCOLOR = 0x0040
    }

    [Flags]
    public enum DBIMF : uint
    {
        NORMAL = 0x0000,
        FIXED = 0x0001,
        FIXEDBMP = 0x0004,
        VARIABLEHEIGHT = 0x0008,
        UNDELETEABLE = 0x0010,
        DEBOSSED = 0x0020,
        BKCOLOR = 0x0040,
        USECHEVRON = 0x0080,
        BREAK = 0x0100,
        ADDTOFRONT = 0x0200,
        TOPALIGN = 0x0400,
        NOGRIPPER = 0x0800,
        ALWAYSGRIPPER = 0x1000,
        NOMARGINS = 0x2000
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public POINTL pt;
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("FC4801A3-2BA9-11CF-A229-00AA003D7352")]
    public interface IObjectWithSite
    {
        [PreserveSig]
        int SetSite([In, MarshalAs(UnmanagedType.IUnknown)] object pUnkSite);
        [PreserveSig]
        int GetSite(ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object ppvSite);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("EB0FE172-1A3A-11D0-89B3-00A0C90A90AC"), SuppressUnmanagedCodeSecurity]
    internal interface IDeskBand
    {
        void GetWindow(out IntPtr phwnd);
        void ContextSensitiveHelp([In] bool fEnterMode);
        void ShowDW([In] bool fShow);
        void CloseDW([In] uint dwReserved);
        void ResizeBorderDW(IntPtr prcBorder, [In, MarshalAs(UnmanagedType.IUnknown)] object punkToolbarSite, bool fReserved);
        void GetBandInfo(uint dwBandID, uint dwViewMode, ref DESKBANDINFO pdbi);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("68284FAA-6A48-11D0-8C78-00C04FD918B4"), SuppressUnmanagedCodeSecurity]
    internal interface IInputObject
    {
        [PreserveSig]
        int UIActivateIO(int fActivate, ref MSG msg);
        [PreserveSig]
        int HasFocusIO();
        [PreserveSig]
        int TranslateAcceleratorIO(ref MSG msg);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("F1DB8392-7331-11D0-8C99-00A0C92DBFE8")]
    internal interface IInputObjectSite
    {
        [PreserveSig]
        int OnFocusChangeIS([MarshalAs(UnmanagedType.IUnknown)] object punkObj, int fSetFocus);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("00000109-0000-0000-C000-000000000046")]
    internal interface IPersistStream
    {
        void GetClassID(out Guid pClassID);
        [PreserveSig]
        int IsDirty();
        void Load([MarshalAs(UnmanagedType.Interface)] object pStm);
        void Save([MarshalAs(UnmanagedType.Interface)] object pStm, [MarshalAs(UnmanagedType.Bool)] bool fClearDirty);
        void GetSizeMax(out ulong pcbSize);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("6D5140C1-7436-11CE-8034-00AA006009FA")]
    internal interface IServiceProvider
    {
        [PreserveSig]
        int QueryService(ref Guid guidService, ref Guid riid, out IntPtr ppvObject);
    }

    public sealed class ExplorerProxy
    {
        private readonly object browser;

        public ExplorerProxy(object browserObject)
        {
            browser = browserObject;
        }

        public string LocationURL
        {
            get
            {
                try
                {
                    object value = browser.GetType().InvokeMember(
                        "LocationURL",
                        BindingFlags.GetProperty,
                        null,
                        browser,
                        null);
                    return value as string;
                }
                catch
                {
                    return null;
                }
            }
        }
    }

    public class BandObject : UserControl, IDeskBand, IInputObject, IObjectWithSite, IPersistStream
    {
        private const int ENotImpl = unchecked((int)0x80004001);
        private IInputObjectSite inputSite;
        private object site;
        private Size minSize = new Size(16, 26);

        protected ExplorerProxy Explorer;

        public Size MinSize
        {
            get { return minSize; }
            set { minSize = value; }
        }

        public virtual int SetSite(object pUnkSite)
        {
            site = pUnkSite;
            inputSite = pUnkSite as IInputObjectSite;
            Explorer = null;

            if (pUnkSite == null)
            {
                return 0;
            }

            IServiceProvider provider = pUnkSite as IServiceProvider;
            if (provider != null)
            {
                Guid sidWebBrowserApp = new Guid("0002DF05-0000-0000-C000-000000000046");
                Guid iidIUnknown = new Guid("00000000-0000-0000-C000-000000000046");
                IntPtr browserPtr;
                if (provider.QueryService(ref sidWebBrowserApp, ref iidIUnknown, out browserPtr) == 0 && browserPtr != IntPtr.Zero)
                {
                    try
                    {
                        object browser = Marshal.GetObjectForIUnknown(browserPtr);
                        Explorer = new ExplorerProxy(browser);
                    }
                    finally
                    {
                        Marshal.Release(browserPtr);
                    }
                }
            }

            OnExplorerAttached();
            return 0;
        }

        public virtual int GetSite(ref Guid riid, out object ppvSite)
        {
            ppvSite = site;
            return site == null ? unchecked((int)0x80004005) : 0;
        }

        protected virtual void OnExplorerAttached()
        {
        }

        public virtual void GetWindow(out IntPtr phwnd)
        {
            phwnd = Handle;
        }

        public virtual void ContextSensitiveHelp(bool fEnterMode)
        {
        }

        public virtual void ShowDW(bool fShow)
        {
            Visible = fShow;
        }

        public virtual void CloseDW(uint dwReserved)
        {
            Visible = false;
            Dispose(true);
        }

        public virtual void ResizeBorderDW(IntPtr prcBorder, object punkToolbarSite, bool fReserved)
        {
        }

        public virtual void GetBandInfo(uint dwBandID, uint dwViewMode, ref DESKBANDINFO pdbi)
        {
            if ((pdbi.dwMask & (uint)DBIM.ACTUAL) != 0)
            {
                pdbi.ptActual.X = Size.Width;
                pdbi.ptActual.Y = Size.Height;
            }
            if ((pdbi.dwMask & (uint)DBIM.MINSIZE) != 0)
            {
                pdbi.ptMinSize.X = MinSize.Width;
                pdbi.ptMinSize.Y = MinSize.Height;
            }
            if ((pdbi.dwMask & (uint)DBIM.MAXSIZE) != 0)
            {
                pdbi.ptMaxSize.X = -1;
                pdbi.ptMaxSize.Y = -1;
            }
            if ((pdbi.dwMask & (uint)DBIM.INTEGRAL) != 0)
            {
                pdbi.ptIntegral.X = -1;
                pdbi.ptIntegral.Y = -1;
            }
            if ((pdbi.dwMask & (uint)DBIM.MODEFLAGS) != 0)
            {
                pdbi.dwModeFlags = DBIMF.NORMAL;
            }
            if ((pdbi.dwMask & (uint)DBIM.TITLE) != 0)
            {
                pdbi.wszTitle = null;
            }
            if ((pdbi.dwMask & (uint)DBIM.BKCOLOR) != 0)
            {
                pdbi.dwMask &= ~(uint)DBIM.BKCOLOR;
            }
        }

        public int UIActivateIO(int fActivate, ref MSG msg)
        {
            if (fActivate != 0)
            {
                Focus();
            }
            return 0;
        }

        public int HasFocusIO()
        {
            return ContainsFocus ? 0 : 1;
        }

        public int TranslateAcceleratorIO(ref MSG msg)
        {
            return 1;
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            if (inputSite != null)
            {
                inputSite.OnFocusChangeIS(this, 1);
            }
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            if (inputSite != null)
            {
                inputSite.OnFocusChangeIS(this, 0);
            }
        }

        public void GetClassID(out Guid pClassID)
        {
            pClassID = GetType().GUID;
        }

        public int IsDirty()
        {
            return 0;
        }

        public void Load(object pStm)
        {
        }

        public void Save(object pStm, bool fClearDirty)
        {
        }

        public void GetSizeMax(out ulong pcbSize)
        {
            pcbSize = 0;
            Marshal.ThrowExceptionForHR(ENotImpl);
        }
    }
}
