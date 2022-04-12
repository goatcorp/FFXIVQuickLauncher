using System;
using System.Diagnostics;
using System.Reflection;
using Microsoft.Win32.SafeHandles;

namespace XIVLauncher.Common;

/// <summary>
/// Class allowing the creation of a Process object based on an already held handle.
/// </summary>
public class ExistingProcess : Process
{
    public ExistingProcess(IntPtr handle)
    {
        SetHandle(handle);
    }

    private void SetHandle(IntPtr handle)
    {
        var baseType = GetType().BaseType;
        if (baseType == null)
            return;

        var setProcessHandleMethod = baseType.GetMethod("SetProcessHandle",
            BindingFlags.NonPublic | BindingFlags.Instance);
        setProcessHandleMethod?.Invoke(this, new object[] { new SafeProcessHandle(handle, true) });
    }
}