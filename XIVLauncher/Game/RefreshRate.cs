using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace XIVLauncher.Game
{
    public class RefreshRate
    {
        public static int? OriginalRefreshRate { get; private set; }

        public static void ChangeRefreshRate(int refreshRate)
        {
            // No refresh rate specified
            if (refreshRate == 0)
                return;

            // Create the object, and since this is passing to a native API,
            // the Size field in the native structure MUST be provided.
            DevModeW devMode = new DevModeW
            {
                dmSize = Convert.ToInt16(Marshal.SizeOf<DevModeW>())
            };

            // Get the current display settings for the display this application
            // is currently on. If this fails, we won't know the name for later
            // or the original refresh rate so we can swap back.
            if (!EnumDisplaySettingsExW(null, -1, ref devMode, 0))
                return;

            // If the refresh rate is already the desired value, no need to continue.
            if (devMode.dmDisplayFrequency == refreshRate)
                return;

            OriginalRefreshRate = devMode.dmDisplayFrequency;

            // Setup the new Device Mode Structure
            devMode = new DevModeW
            {
                dmSize = Convert.ToInt16(Marshal.SizeOf<DevModeW>()),   // Have to tell native API the size of the structure
                dmDisplayFrequency = refreshRate,                       // This telling them what refresh rate we want
                dmFields = 0x00400000                                   // This tells the WinAPI we are only changing the refresh rate. This is the DM_DISPLAYFREQUENCY constant.
            };

            // Change the Display Device's Mode, if it isn't successful clear the saved Refresh Rate
            if (ChangeDisplaySettingsW(ref devMode, 0) != 0)
            {
                OriginalRefreshRate = null;
            }
        }

        public static void RestoreRefreshRate()
        {
            if (!OriginalRefreshRate.HasValue)
                return; // No Pre-change Settings we can restore

            // Setup the new Device Mode Structure
            DevModeW devMode = new DevModeW
            {
                dmSize = Convert.ToInt16(Marshal.SizeOf<DevModeW>()),   // Have to tell native API the size of the structure
                dmDisplayFrequency = OriginalRefreshRate.Value,         // This telling them what refresh rate we want
                dmFields = 0x00400000                                   // This tells the WinAPI we are only changing the refresh rate. This is the DM_DISPLAYFREQUENCY constant.
            };

            // Change the Display Device's Mode, if it isn't successful clear the saved Refresh Rate
            ChangeDisplaySettingsW(ref devMode, 0);
        }

        // Constants used for native data structures
        private const int CCHDEVICENAME = 32;
        private const int CCHFORMNAME = 32;

        // Used to get the Display Settings
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumDisplaySettingsExW(
            string lpszDeviceName,
            int iModeNum,
            ref DevModeW lpDevMode,
            int dwFlags);

        // Changes the Display Mode for the Default (Primary) Display
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int ChangeDisplaySettingsW(
            ref DevModeW lpDevMode,
            int dwflags);

        // This structure is used to reprent a Display Device's settings
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DevModeW
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCHDEVICENAME)]
            public string dmDeviceName;
            public short dmSpecVersion;
            public short dmDriverVersion;
            public short dmSize;
            public short dmDriverExtra;
            public int dmFields;
            public int x;
            public int y;
            public int dmDisplayOrientation;
            public int dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCHFORMNAME)]
            public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel;
            public int dmPelsWidth;
            public int dmPelsHeight;
            public int dmDisplayFlags;
            public int dmDisplayFrequency;
            public int dmICMMethod;
            public int dmICMIntent;
            public int dmMediaType;
            public int dmDitherType;
            public int dmReserved1;
            public int dmReserved2;
            public int dmPanningWidth;
            public int dmPanningHeight;
        }
    }
}
