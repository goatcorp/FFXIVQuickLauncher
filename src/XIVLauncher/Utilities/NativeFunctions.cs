using System.Runtime.InteropServices;

namespace XIVLauncher.Utilities;

internal class NativeFunctions
{
    [DllImport("shell32.dll", SetLastError = true)]
    internal static extern void SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string appId);
}
