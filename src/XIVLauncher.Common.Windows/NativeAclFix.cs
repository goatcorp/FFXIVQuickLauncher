using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using Serilog;
using XIVLauncher.Common.Game.Exceptions;

// ReSharper disable InconsistentNaming

namespace XIVLauncher.Common.Game
{
    public static class NativeAclFix
    {
        // Definitions taken from PInvoke.net (with some changes)
        private static class PInvoke
        {
            #region Constants
            public const UInt32 STANDARD_RIGHTS_ALL = 0x001F0000;
            public const UInt32 SPECIFIC_RIGHTS_ALL = 0x0000FFFF;
            public const UInt32 PROCESS_VM_WRITE = 0x0020;

            public const UInt32 GRANT_ACCESS = 1;

            public const UInt32 SECURITY_DESCRIPTOR_REVISION = 1;

            public const UInt32 CREATE_SUSPENDED = 0x00000004;

            public const UInt32 TOKEN_QUERY = 0x0008;
            public const UInt32 TOKEN_ADJUST_PRIVILEGES = 0x0020;

            public const UInt32 PRIVILEGE_SET_ALL_NECESSARY = 1;

            public const UInt32 SE_PRIVILEGE_ENABLED = 0x00000002;
            public const UInt32 SE_PRIVILEGE_REMOVED = 0x00000004;


            public enum MULTIPLE_TRUSTEE_OPERATION
            {
                NO_MULTIPLE_TRUSTEE,
                TRUSTEE_IS_IMPERSONATE
            }

            public enum TRUSTEE_FORM
            {
                TRUSTEE_IS_SID,
                TRUSTEE_IS_NAME,
                TRUSTEE_BAD_FORM,
                TRUSTEE_IS_OBJECTS_AND_SID,
                TRUSTEE_IS_OBJECTS_AND_NAME
            }

            public enum TRUSTEE_TYPE
            {
                TRUSTEE_IS_UNKNOWN,
                TRUSTEE_IS_USER,
                TRUSTEE_IS_GROUP,
                TRUSTEE_IS_DOMAIN,
                TRUSTEE_IS_ALIAS,
                TRUSTEE_IS_WELL_KNOWN_GROUP,
                TRUSTEE_IS_DELETED,
                TRUSTEE_IS_INVALID,
                TRUSTEE_IS_COMPUTER
            }

            public enum SE_OBJECT_TYPE
            {
                SE_UNKNOWN_OBJECT_TYPE,
                SE_FILE_OBJECT,
                SE_SERVICE,
                SE_PRINTER,
                SE_REGISTRY_KEY,
                SE_LMSHARE,
                SE_KERNEL_OBJECT,
                SE_WINDOW_OBJECT,
                SE_DS_OBJECT,
                SE_DS_OBJECT_ALL,
                SE_PROVIDER_DEFINED_OBJECT,
                SE_WMIGUID_OBJECT,
                SE_REGISTRY_WOW64_32KEY
            }
            public enum SECURITY_INFORMATION
            {
                OWNER_SECURITY_INFORMATION = 1,
                GROUP_SECURITY_INFORMATION = 2,
                DACL_SECURITY_INFORMATION = 4,
                SACL_SECURITY_INFORMATION = 8,
                UNPROTECTED_SACL_SECURITY_INFORMATION = 0x10000000,
                UNPROTECTED_DACL_SECURITY_INFORMATION = 0x20000000,
                PROTECTED_SACL_SECURITY_INFORMATION = 0x40000000
            }
            #endregion


            #region Structures
            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto, Pack = 0)]
            public struct TRUSTEE : IDisposable
            {
                public IntPtr pMultipleTrustee;
                public MULTIPLE_TRUSTEE_OPERATION MultipleTrusteeOperation;
                public TRUSTEE_FORM TrusteeForm;
                public TRUSTEE_TYPE TrusteeType;
                private IntPtr ptstrName;

                void IDisposable.Dispose()
                {
                    if (ptstrName != IntPtr.Zero) Marshal.Release(ptstrName);
                }

                public string Name { get { return Marshal.PtrToStringAuto(ptstrName); } }
            }

            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto, Pack = 0)]
            public struct EXPLICIT_ACCESS
            {
                uint grfAccessPermissions;
                uint grfAccessMode;
                uint grfInheritance;
                TRUSTEE Trustee;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct SECURITY_DESCRIPTOR
            {
                public byte Revision;
                public byte Sbz1;
                public UInt16 Control;
                public IntPtr Owner;
                public IntPtr Group;
                public IntPtr Sacl;
                public IntPtr Dacl;
            }

            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
            public struct STARTUPINFO
            {
                public Int32 cb;
                public string lpReserved;
                public string lpDesktop;
                public string lpTitle;
                public Int32 dwX;
                public Int32 dwY;
                public Int32 dwXSize;
                public Int32 dwYSize;
                public Int32 dwXCountChars;
                public Int32 dwYCountChars;
                public Int32 dwFillAttribute;
                public Int32 dwFlags;
                public Int16 wShowWindow;
                public Int16 cbReserved2;
                public IntPtr lpReserved2;
                public IntPtr hStdInput;
                public IntPtr hStdOutput;
                public IntPtr hStdError;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct PROCESS_INFORMATION
            {
                public IntPtr hProcess;
                public IntPtr hThread;
                public int dwProcessId;
                public UInt32 dwThreadId;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct SECURITY_ATTRIBUTES
            {
                public int nLength;
                public IntPtr lpSecurityDescriptor;
                public bool bInheritHandle;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct LUID
            {
                public UInt32 LowPart;
                public Int32 HighPart;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct PRIVILEGE_SET
            {
                public UInt32 PrivilegeCount;
                public UInt32 Control;
                [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
                public LUID_AND_ATTRIBUTES[] Privilege;
            }

            public struct LUID_AND_ATTRIBUTES
            {
                public LUID Luid;
                public UInt32 Attributes;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct TOKEN_PRIVILEGES
            {
                public UInt32 PrivilegeCount;
                [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
                public LUID_AND_ATTRIBUTES[] Privileges;
            }
            #endregion


            #region Methods
            [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
            public static extern void BuildExplicitAccessWithName(
                ref EXPLICIT_ACCESS pExplicitAccess,
                string pTrusteeName,
                uint AccessPermissions,
                uint AccessMode,
                uint Inheritance);

            [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
            public static extern int SetEntriesInAcl(
                int cCountOfExplicitEntries,
                ref EXPLICIT_ACCESS pListOfExplicitEntries,
                IntPtr OldAcl,
                out IntPtr NewAcl);

            [DllImport("advapi32.dll", SetLastError = true)]
            public static extern bool InitializeSecurityDescriptor(
                out SECURITY_DESCRIPTOR pSecurityDescriptor,
                uint dwRevision);

            [DllImport("advapi32.dll", SetLastError = true)]
            public static extern bool SetSecurityDescriptorDacl(
                ref SECURITY_DESCRIPTOR pSecurityDescriptor,
                bool bDaclPresent,
                IntPtr pDacl,
                bool bDaclDefaulted);

            [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
            public static extern bool CreateProcess(
               string lpApplicationName,
               string lpCommandLine,
               ref SECURITY_ATTRIBUTES lpProcessAttributes,
               IntPtr lpThreadAttributes,
               bool bInheritHandles,
               UInt32 dwCreationFlags,
               IntPtr lpEnvironment,
               string lpCurrentDirectory,
               [In] ref STARTUPINFO lpStartupInfo,
               out PROCESS_INFORMATION lpProcessInformation);

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern bool CloseHandle(IntPtr hObject);

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern uint ResumeThread(IntPtr hThread);

            [DllImport("advapi32.dll", SetLastError = true)]
            public static extern bool OpenProcessToken(
                IntPtr ProcessHandle,
                UInt32 DesiredAccess,
                out IntPtr TokenHandle);

            [DllImport("advapi32.dll", SetLastError = true)]
            public static extern bool LookupPrivilegeValue(string lpSystemName, string lpName, ref LUID lpLuid);

            [DllImport("advapi32.dll", SetLastError = true)]
            public static extern bool PrivilegeCheck(
                IntPtr ClientToken,
                ref PRIVILEGE_SET RequiredPrivileges,
                out bool pfResult);

            [DllImport("advapi32.dll", SetLastError = true)]
            public static extern bool AdjustTokenPrivileges(
                IntPtr TokenHandle,
                bool DisableAllPrivileges,
                ref TOKEN_PRIVILEGES NewState,
                UInt32 BufferLengthInBytes,
                IntPtr PreviousState,
                UInt32 ReturnLengthInBytes);

            [DllImport("advapi32.dll", SetLastError = true)]
            public static extern uint GetSecurityInfo(
                IntPtr handle,
                SE_OBJECT_TYPE ObjectType,
                SECURITY_INFORMATION SecurityInfo,
                IntPtr pSidOwner,
                IntPtr pSidGroup,
                out IntPtr pDacl,
                IntPtr pSacl,
                IntPtr pSecurityDescriptor);

            [DllImport("advapi32.dll", SetLastError = true)]
            public static extern uint SetSecurityInfo(
                IntPtr handle,
                SE_OBJECT_TYPE ObjectType,
                SECURITY_INFORMATION SecurityInfo,
                IntPtr psidOwner,
                IntPtr psidGroup,
                IntPtr pDacl,
                IntPtr pSacl);

            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern IntPtr GetCurrentProcess();
            #endregion
        }

        public static Process LaunchGame(string workingDir, string exePath, string arguments, IDictionary<string, string> envVars, DpiAwareness dpiAwareness, Action<Process> beforeResume)
        {
            Process process = null;

            var userName = Environment.UserName;

            var pExplicitAccess = new PInvoke.EXPLICIT_ACCESS();
            PInvoke.BuildExplicitAccessWithName(
                ref pExplicitAccess,
                userName,
                PInvoke.STANDARD_RIGHTS_ALL | PInvoke.SPECIFIC_RIGHTS_ALL & ~PInvoke.PROCESS_VM_WRITE,
                PInvoke.GRANT_ACCESS,
                0);

            if (PInvoke.SetEntriesInAcl(1, ref pExplicitAccess, IntPtr.Zero, out var newAcl) != 0)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            var secDesc = new PInvoke.SECURITY_DESCRIPTOR();

            if (!PInvoke.InitializeSecurityDescriptor(out secDesc, PInvoke.SECURITY_DESCRIPTOR_REVISION))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            if (!PInvoke.SetSecurityDescriptorDacl(ref secDesc, true, newAcl, false))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            var psecDesc = Marshal.AllocHGlobal(Marshal.SizeOf<PInvoke.SECURITY_DESCRIPTOR>());
            Marshal.StructureToPtr<PInvoke.SECURITY_DESCRIPTOR>(secDesc, psecDesc, true);

            var lpProcessInformation = new PInvoke.PROCESS_INFORMATION();
            var lpEnvironment = IntPtr.Zero;

            try
            {
                if (envVars.Count > 0)
                {
                    string envstr = string.Join("\0", envVars.Select(entry => entry.Key + "=" + entry.Value));

                    lpEnvironment = Marshal.StringToHGlobalAnsi(envstr);
                }

                var lpProcessAttributes = new PInvoke.SECURITY_ATTRIBUTES
                {
                    nLength = Marshal.SizeOf<PInvoke.SECURITY_ATTRIBUTES>(),
                    lpSecurityDescriptor = psecDesc,
                    bInheritHandle = false
                };

                var lpStartupInfo = new PInvoke.STARTUPINFO
                {
                    cb = Marshal.SizeOf<PInvoke.STARTUPINFO>()
                };

                var compatLayerPrev = Environment.GetEnvironmentVariable("__COMPAT_LAYER");

                var compat = "RunAsInvoker ";
                compat += dpiAwareness switch
                {
                    DpiAwareness.Aware => "HighDPIAware",
                    DpiAwareness.Unaware => "DPIUnaware",
                    _ => throw new ArgumentOutOfRangeException()
                };
                Environment.SetEnvironmentVariable("__COMPAT_LAYER", compat);

                if (!PInvoke.CreateProcess(
                        null,
                        $"\"{exePath}\" {arguments}",
                        ref lpProcessAttributes,
                        IntPtr.Zero,
                        false,
                        PInvoke.CREATE_SUSPENDED,
                        IntPtr.Zero,
                        workingDir,
                        ref lpStartupInfo,
                        out lpProcessInformation))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                Environment.SetEnvironmentVariable("__COMPAT_LAYER", compatLayerPrev);

                DisableSeDebug(lpProcessInformation.hProcess);

                process = new ExistingProcess(lpProcessInformation.hProcess);

                beforeResume?.Invoke(process);

                PInvoke.ResumeThread(lpProcessInformation.hThread);

                // Ensure that the game main window is prepared
                try
                {
                    do
                    {
                        process.WaitForInputIdle();

                        Thread.Sleep(100);
                    } while (IntPtr.Zero == TryFindGameWindow(process));
                }
                catch (InvalidOperationException)
                {
                    throw new GameExitedException();
                }

                if (PInvoke.GetSecurityInfo(
                        PInvoke.GetCurrentProcess(),
                        PInvoke.SE_OBJECT_TYPE.SE_KERNEL_OBJECT,
                        PInvoke.SECURITY_INFORMATION.DACL_SECURITY_INFORMATION,
                        IntPtr.Zero, IntPtr.Zero,
                        out var pACL,
                        IntPtr.Zero, IntPtr.Zero) != 0)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                if (PInvoke.SetSecurityInfo(
                        lpProcessInformation.hProcess,
                        PInvoke.SE_OBJECT_TYPE.SE_KERNEL_OBJECT,
                        PInvoke.SECURITY_INFORMATION.DACL_SECURITY_INFORMATION |
                        PInvoke.SECURITY_INFORMATION.UNPROTECTED_DACL_SECURITY_INFORMATION,
                        IntPtr.Zero, IntPtr.Zero, pACL, IntPtr.Zero) != 0)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[NativeAclFix] Uncaught error during initialization, trying to kill process");

                try
                {
                    process?.Kill();
                }
                catch (Exception killEx)
                {
                    Log.Error(killEx, "[NativeAclFix] Could not kill process");
                }

                throw;
            }
            finally
            {
                Marshal.FreeHGlobal(psecDesc);

                if (!IntPtr.Equals(lpEnvironment, IntPtr.Zero))
                {
                    Marshal.FreeHGlobal(lpEnvironment);
                }

                PInvoke.CloseHandle(lpProcessInformation.hThread);
            }

            return process;
        }

        private static void DisableSeDebug(IntPtr ProcessHandle)
        {
            if (!PInvoke.OpenProcessToken(ProcessHandle, PInvoke.TOKEN_QUERY | PInvoke.TOKEN_ADJUST_PRIVILEGES, out var TokenHandle))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            var luidDebugPrivilege = new PInvoke.LUID();
            if (!PInvoke.LookupPrivilegeValue(null, "SeDebugPrivilege", ref luidDebugPrivilege))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            var RequiredPrivileges = new PInvoke.PRIVILEGE_SET
            {
                PrivilegeCount = 1,
                Control = PInvoke.PRIVILEGE_SET_ALL_NECESSARY,
                Privilege = new PInvoke.LUID_AND_ATTRIBUTES[1]
            };

            RequiredPrivileges.Privilege[0].Luid = luidDebugPrivilege;
            RequiredPrivileges.Privilege[0].Attributes = PInvoke.SE_PRIVILEGE_ENABLED;

            if (!PInvoke.PrivilegeCheck(TokenHandle, ref RequiredPrivileges, out bool bResult))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            if (bResult) // SeDebugPrivilege is enabled; try disabling it
            {
                var TokenPrivileges = new PInvoke.TOKEN_PRIVILEGES
                {
                    PrivilegeCount = 1,
                    Privileges = new PInvoke.LUID_AND_ATTRIBUTES[1]
                };

                TokenPrivileges.Privileges[0].Luid = luidDebugPrivilege;
                TokenPrivileges.Privileges[0].Attributes = PInvoke.SE_PRIVILEGE_REMOVED;

                if (!PInvoke.AdjustTokenPrivileges(TokenHandle, false, ref TokenPrivileges, 0, IntPtr.Zero, 0))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
            }

            PInvoke.CloseHandle(TokenHandle);
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindowEx(IntPtr parentHandle, IntPtr hWndChildAfter, string className, IntPtr windowTitle);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool IsWindowVisible(IntPtr hWnd);

        private static IntPtr TryFindGameWindow(Process process)
        {
            IntPtr hwnd = IntPtr.Zero;
            while (IntPtr.Zero != (hwnd = FindWindowEx(IntPtr.Zero, hwnd, "FFXIVGAME", IntPtr.Zero)))
            {
                GetWindowThreadProcessId(hwnd, out uint pid);

                if (pid == process.Id && IsWindowVisible(hwnd))
                {
                    break;
                }
            }
            return hwnd;
        }
    }
}