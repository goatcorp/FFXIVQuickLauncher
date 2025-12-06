using System.Runtime.Versioning;
using Microsoft.Win32;

namespace XIVLauncher.Common.Windows.Shell;

[SupportedOSPlatform("windows")]
internal class FileAssociation(string extension, string description, string handlerExePath, string? iconPath = null)
{
    private const string SoftwareClasses = @"Software\Classes";

    /// <summary>
    /// Sub key for setting the icon.
    /// https://learn.microsoft.com/en-us/windows/win32/com/defaulticon
    /// </summary>
    private const string DefaultIcon = "DefaultIcon";

    /// <summary>
    /// Sub key for setting the command line that the shell invokes.
    /// https://learn.microsoft.com/en-us/windows/win32/com/shell
    /// </summary>
    internal const string SHELL_OPEN_COMMAND = @"Shell\Open\Command";

    public string ProgramId => $"{WindowsAssociationManager.PROGRAM_ID_PREFIX}{Extension}";

    public string Extension { get; } = extension;
    private string Description { get; } = description;
    private string IconPath { get; } = iconPath ?? $"{handlerExePath},1"; // default to exe icon
    private string HandlerExePath { get; } = handlerExePath;

    /// <summary>
    /// Installs a file extension association in accordance with https://learn.microsoft.com/en-us/windows/win32/com/-progid--key
    /// </summary>
    public void Install()
    {
        using var classes = Registry.CurrentUser.OpenSubKey(SoftwareClasses, true);
        if (classes == null) return;

        // register a program id for the given extension
        using (var programKey = classes.CreateSubKey(ProgramId))
        {
            programKey.SetValue(null, this.Description);

            using (var defaultIconKey = programKey.CreateSubKey(DefaultIcon))
                defaultIconKey.SetValue(null, this.IconPath);

            using (var openCommandKey = programKey.CreateSubKey(SHELL_OPEN_COMMAND))
                openCommandKey.SetValue(null, $"""{this.HandlerExePath}"" ""%1""");
        }

        using (var extensionKey = classes.CreateSubKey(Extension))
        {
            // Clear out our existing default ProgramID. Default programs in Windows are handled internally by Explorer,
            // so having it here is just confusing and may override user preferences.
            if (extensionKey.GetValue(null) is string s && s == ProgramId)
                extensionKey.SetValue(null, string.Empty);

            // add to the open with dialog
            // https://learn.microsoft.com/en-us/windows/win32/shell/how-to-include-an-application-on-the-open-with-dialog-box
            using (var openWithKey = extensionKey.CreateSubKey("OpenWithProgIds"))
                openWithKey.SetValue(ProgramId, string.Empty);
        }
    }

    /// <summary>
    /// Uninstalls the file extension association in accordance with https://learn.microsoft.com/en-us/windows/win32/shell/fa-file-types#deleting-registry-information-during-uninstallation
    /// </summary>
    public void Uninstall()
    {
        using var classes = Registry.CurrentUser.OpenSubKey(SoftwareClasses, true);
        if (classes == null) return;

        using (var extensionKey = classes.OpenSubKey(Extension, true))
        {
            using (var openWithKey = extensionKey?.CreateSubKey("OpenWithProgIds"))
                openWithKey?.DeleteValue(ProgramId, throwOnMissingValue: false);
        }

        classes.DeleteSubKeyTree(ProgramId, throwOnMissingSubKey: false);
    }
}
