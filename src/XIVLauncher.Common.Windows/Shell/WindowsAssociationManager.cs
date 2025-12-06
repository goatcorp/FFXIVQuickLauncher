// Stolen from Osu! (https://github.com/ppy/osu/blob/master/osu.Desktop/Windows/WindowsAssociationManager.cs) under MIT license.
// Adapted and "cleaned" for our use case.

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Serilog;

namespace XIVLauncher.Common.Windows.Shell;

[SupportedOSPlatform("windows")]
public static class WindowsAssociationManager
{
    private static readonly string AppDirectory = Path.GetDirectoryName(typeof(WindowsAssociationManager).Assembly.Location) ?? string.Empty;

    /// <summary>
    /// Program ID prefix used for file associations. Should be relatively short since the full program ID has a 39 character limit,
    /// see https://learn.microsoft.com/en-us/windows/win32/com/-progid--key.
    /// </summary>
    internal const string PROGRAM_ID_PREFIX = "XIVLauncher";

    private static readonly ApplicationCapability CapabilityObject = new("xivlauncher", @"Software\Goatcorp\XIVLauncher\Capabilities", "XIVLauncher");

    private static readonly UriAssociation[] UriAssociations =
    [
        new(@"dalamud", "Dalamud RPC URI Scheme", Path.Join(AppDirectory, "linkhandler", "XIVLauncher.LinkHandler.exe"))
    ];

    private static readonly FileAssociation[] FileAssociations = [];

    /// <summary>
    /// Installs file and URI associations.
    /// Call at both initial install and updates.
    /// </summary>
    public static void InstallAssociations()
    {
        try
        {
            RegisterAssociations();
            NotifyShellUpdate();
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to install file and URI associations: {EMessage}", e.Message);
        }
    }

    public static void UninstallAssociations()
    {
        try
        {
            CapabilityObject.Uninstall();

            foreach (var association in UriAssociations)
                association.Uninstall();

            foreach (var association in FileAssociations)
                association.Uninstall();

            NotifyShellUpdate();
        }
        catch (Exception e)
        {
            Log.Error(e, @"Failed to uninstall file and URI associations.");
        }
    }

    public static void NotifyShellUpdate()
    {
        SHChangeNotify(EventId.SHCNE_ASSOCCHANGED, Flags.SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
    }

    /// <summary>
    /// Installs or updates associations.
    /// </summary>
    private static void RegisterAssociations()
    {
        CapabilityObject.Install();

        foreach (var uriAssociation in UriAssociations)
            uriAssociation.Install();

        foreach (var fileAssociation in FileAssociations)
            fileAssociation.Install();

        CapabilityObject.RegisterUriAssociations(UriAssociations);
        CapabilityObject.RegisterFileAssociations(FileAssociations);
    }

    #region Native interop

    [DllImport("Shell32.dll")]
    private static extern void SHChangeNotify(EventId wEventId, Flags uFlags, IntPtr dwItem1, IntPtr dwItem2);

    [SuppressMessage("ReSharper", "InconsistentNaming")]
    private enum EventId
    {
        /// <summary>
        /// A file type association has changed. <see cref="Flags.SHCNF_IDLIST"/> must be specified in the uFlags parameter.
        /// dwItem1 and dwItem2 are not used and must be <see cref="IntPtr.Zero"/>. This event should also be sent for registered protocols.
        /// </summary>
        SHCNE_ASSOCCHANGED = 0x08000000
    }

    [SuppressMessage("ReSharper", "InconsistentNaming")]
    private enum Flags : uint
    {
        SHCNF_IDLIST = 0x0000
    }

    #endregion
}
