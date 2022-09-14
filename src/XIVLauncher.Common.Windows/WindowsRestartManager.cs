using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using Exception = System.Exception;

// ReSharper disable FieldCanBeMadeReadOnly.Local
// ReSharper disable MemberCanBePrivate.Local

namespace XIVLauncher.Common.Windows;

public class WindowsRestartManager : IDisposable
{
    public delegate void RmWriteStatusCallback(uint percentageCompleted);

    private const int RM_SESSION_KEY_LEN = 16; // sizeof GUID
    private const int CCH_RM_SESSION_KEY = RM_SESSION_KEY_LEN * 2;
    private const int CCH_RM_MAX_APP_NAME = 255;
    private const int CCH_RM_MAX_SVC_NAME = 63;
    private const int RM_INVALID_TS_SESSION = -1;
    private const int RM_INVALID_PROCESS = -1;
    private const int ERROR_MORE_DATA = 234;

    [StructLayout(LayoutKind.Sequential)]
    public struct RmUniqueProcess
    {
        public int dwProcessId; // PID
        public FILETIME ProcessStartTime; // Process creation time
    }

    public enum RmAppType
    {
        /// <summary>
        /// Application type cannot be classified in known categories
        /// </summary>
        RmUnknownApp = 0,

        /// <summary>
        /// Application is a windows application that displays a top-level window
        /// </summary>
        RmMainWindow = 1,

        /// <summary>
        /// Application is a windows app but does not display a top-level window
        /// </summary>
        RmOtherWindow = 2,

        /// <summary>
        /// Application is an NT service
        /// </summary>
        RmService = 3,

        /// <summary>
        /// Application is Explorer
        /// </summary>
        RmExplorer = 4,

        /// <summary>
        /// Application is Console application
        /// </summary>
        RmConsole = 5,

        /// <summary>
        /// Application is critical system process where a reboot is required to restart
        /// </summary>
        RmCritical = 1000,
    }

    [Flags]
    public enum RmRebootReason
    {
        /// <summary>
        /// A system restart is not required.
        /// </summary>
        RmRebootReasonNone = 0x0,

        /// <summary>
        /// The current user does not have sufficient privileges to shut down one or more processes.
        /// </summary>
        RmRebootReasonPermissionDenied = 0x1,

        /// <summary>
        /// One or more processes are running in another Terminal Services session.
        /// </summary>
        RmRebootReasonSessionMismatch = 0x2,

        /// <summary>
        /// A system restart is needed because one or more processes to be shut down are critical processes.
        /// </summary>
        RmRebootReasonCriticalProcess = 0x4,

        /// <summary>
        /// A system restart is needed because one or more services to be shut down are critical services.
        /// </summary>
        RmRebootReasonCriticalService = 0x8,

        /// <summary>
        /// A system restart is needed because the current process must be shut down.
        /// </summary>
        RmRebootReasonDetectedSelf = 0x10,
    }

    [Flags]
    private enum RmShutdownType
    {
        RmForceShutdown = 0x1, // Force app shutdown
        RmShutdownOnlyRegistered = 0x10 // Only shutdown apps if all apps registered for restart
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct RmProcessInfo
    {
        public RmUniqueProcess UniqueProcess;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCH_RM_MAX_APP_NAME + 1)]
        public string AppName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCH_RM_MAX_SVC_NAME + 1)]
        public string ServiceShortName;

        public RmAppType ApplicationType;
        public int AppStatus;
        public int TSSessionId;

        [MarshalAs(UnmanagedType.Bool)]
        public bool bRestartable;

        public Process Process
        {
            get
            {
                try
                {
                    Process process = Process.GetProcessById(UniqueProcess.dwProcessId);
                    long fileTime = process.StartTime.ToFileTime();

                    if ((uint)UniqueProcess.ProcessStartTime.dwLowDateTime != (uint)(fileTime & uint.MaxValue))
                        return null;

                    if ((uint)UniqueProcess.ProcessStartTime.dwHighDateTime != (uint)(fileTime >> 32))
                        return null;

                    return process;
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }
    }

    [DllImport("rstrtmgr", CharSet = CharSet.Unicode)]
    private static extern int RmStartSession(out int dwSessionHandle, int sessionFlags, StringBuilder strSessionKey);

    [DllImport("rstrtmgr")]
    private static extern int RmEndSession(int dwSessionHandle);

    [DllImport("rstrtmgr")]
    private static extern int RmShutdown(int dwSessionHandle, RmShutdownType lAtionFlags, RmWriteStatusCallback fnStatus);

    [DllImport("rstrtmgr")]
    private static extern int RmRestart(int dwSessionHandle, int dwRestartFlags, RmWriteStatusCallback fnStatus);

    [DllImport("rstrtmgr")]
    private static extern int RmGetList(int dwSessionHandle, out int nProcInfoNeeded, ref int nProcInfo, [In, Out] RmProcessInfo[] rgAffectedApps, out RmRebootReason dwRebootReasons);

    [DllImport("rstrtmgr", CharSet = CharSet.Unicode)]
    private static extern int RmRegisterResources(int dwSessionHandle,
                                                  int nFiles, string[] rgsFileNames,
                                                  int nApplications, RmUniqueProcess[] rgApplications,
                                                  int nServices, string[] rgsServiceNames);

    private readonly int sessionHandle;
    private readonly string sessionKey;

    public WindowsRestartManager()
    {
        var sessKey = new StringBuilder(CCH_RM_SESSION_KEY + 1);
        ThrowOnFailure(RmStartSession(out sessionHandle, 0, sessKey));
        sessionKey = sessKey.ToString();
    }

    public void Register(IEnumerable<FileInfo> files = null, IEnumerable<Process> processes = null, IEnumerable<string> serviceNames = null)
    {
        string[] filesArray = files?.Select(f => f.FullName).ToArray() ?? Array.Empty<string>();
        RmUniqueProcess[] processesArray = processes?.Select(f => new RmUniqueProcess
        {
            dwProcessId = f.Id,
            ProcessStartTime = new FILETIME
            {
                dwLowDateTime = (int)(f.StartTime.ToFileTime() & uint.MaxValue),
                dwHighDateTime = (int)(f.StartTime.ToFileTime() >> 32),
            }
        }).ToArray() ?? Array.Empty<RmUniqueProcess>();
        string[] servicesArray = serviceNames?.ToArray() ?? Array.Empty<string>();
        ThrowOnFailure(RmRegisterResources(sessionHandle,
            filesArray.Length, filesArray,
            processesArray.Length, processesArray,
            servicesArray.Length, servicesArray));
    }

    public void Shutdown(bool forceShutdown = true, bool shutdownOnlyRegistered = false, RmWriteStatusCallback cb = null)
    {
        ThrowOnFailure(RmShutdown(sessionHandle, (forceShutdown ? RmShutdownType.RmForceShutdown : 0) | (shutdownOnlyRegistered ? RmShutdownType.RmShutdownOnlyRegistered : 0), cb));
    }

    public void Restart(RmWriteStatusCallback cb = null)
    {
        ThrowOnFailure(RmRestart(sessionHandle, 0, cb));
    }

    public List<RmProcessInfo> GetInterferingProcesses(out RmRebootReason rebootReason)
    {
        var count = 0;
        var infos = new RmProcessInfo[count];
        var err = 0;

        for (var i = 0; i < 16; i++)
        {
            err = RmGetList(sessionHandle, out int needed, ref count, infos, out rebootReason);

            switch (err)
            {
                case 0:
                    return infos.Take(count).ToList();

                case ERROR_MORE_DATA:
                    infos = new RmProcessInfo[count = needed];
                    break;

                default:
                    ThrowOnFailure(err);
                    break;
            }
        }

        ThrowOnFailure(err);

        // should not reach
        throw new InvalidOperationException();
    }

    private void ReleaseUnmanagedResources()
    {
        ThrowOnFailure(RmEndSession(sessionHandle));
    }

    public void Dispose()
    {
        ReleaseUnmanagedResources();
        GC.SuppressFinalize(this);
    }

    ~WindowsRestartManager()
    {
        ReleaseUnmanagedResources();
    }

    private void ThrowOnFailure(int err)
    {
        if (err != 0)
            throw new Win32Exception(err);
    }
}
