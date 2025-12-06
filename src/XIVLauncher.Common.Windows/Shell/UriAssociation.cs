using System.Runtime.Versioning;
using Microsoft.Win32;

namespace XIVLauncher.Common.Windows.Shell;

[SupportedOSPlatform("windows")]
internal class UriAssociation(string protocol, string description, string handlerExePath, string? iconPath = null)
{
    private const string SoftwareClasses = @"Software\Classes";

    /// <see href="https://learn.microsoft.com/en-us/windows/win32/com/defaulticon" />
    private const string DefaultIcon = "DefaultIcon";

    /// <see href="https://learn.microsoft.com/en-us/windows/win32/com/shell" />
    internal const string SHELL_OPEN_COMMAND = @"Shell\Open\Command";

    /// <see href="https://learn.microsoft.com/en-us/previous-versions/windows/internet-explorer/ie-developer/platform-apis/aa767914(v=vs.85)"/>
    private const string UrlProtocol = "URL Protocol";

    public string Protocol { get; } = protocol;
    private string Description { get; } = description;
    private string HandlerExePath { get; } = handlerExePath;
    private string IconPath { get; } = iconPath ?? $"{handlerExePath},1";

    public string ProgramId => $"{WindowsAssociationManager.PROGRAM_ID_PREFIX}.{this.Protocol}";

    /// <summary>
    /// Registers an URI protocol handler in accordance with https://learn.microsoft.com/en-us/previous-versions/windows/internet-explorer/ie-developer/platform-apis/aa767914(v=vs.85).
    /// </summary>
    public void Install()
    {
        using var classes = Registry.CurrentUser.OpenSubKey(SoftwareClasses, true);
        if (classes == null) return;

        using (var protocolKey = classes.CreateSubKey(this.Protocol))
        {
            protocolKey.SetValue(null, $"URL:{this.Description}");
            protocolKey.SetValue(UrlProtocol, string.Empty);

            // clear out old data
            protocolKey.DeleteSubKeyTree(DefaultIcon, throwOnMissingSubKey: false);
            protocolKey.DeleteSubKeyTree("Shell", throwOnMissingSubKey: false);
        }

        // register a program id for the given protocol
        using (var programKey = classes.CreateSubKey(this.ProgramId))
        {
            using (var defaultIconKey = programKey.CreateSubKey(DefaultIcon))
                defaultIconKey.SetValue(null, this.IconPath);

            using (var openCommandKey = programKey.CreateSubKey(SHELL_OPEN_COMMAND))
            {
                openCommandKey.SetValue(null, $"""
                                               "{this.HandlerExePath}" "%1"
                                               """);
            }
        }
    }

    public void Uninstall()
    {
        using var classes = Registry.CurrentUser.OpenSubKey(SoftwareClasses, true);
        classes?.DeleteSubKeyTree(this.ProgramId, throwOnMissingSubKey: false);
    }
}
