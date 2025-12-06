using System.Runtime.Versioning;
using Microsoft.Win32;

namespace XIVLauncher.Common.Windows.Shell;

[SupportedOSPlatform("windows")]
internal class ApplicationCapability
{
    private const string SoftwareRegisteredApplications = @"Software\RegisteredApplications";

    private string UniqueName { get; }
    private string CapabilityPath { get; }
    private string Description { get; }

    public ApplicationCapability(string uniqueName, string capabilityPath, string description)
    {
        this.UniqueName = uniqueName;
        this.CapabilityPath = capabilityPath;
        this.Description = description;
    }

    /// <summary>
    /// Registers an application capability according to <see href="https://learn.microsoft.com/en-us/windows/win32/shell/default-programs#registering-an-application-for-use-with-default-programs">
    /// Registering an Application for Use with Default Programs</see>.
    /// </summary>
    public void Install()
    {
        using (var capability = Registry.CurrentUser.CreateSubKey(this.CapabilityPath))
        {
            capability.SetValue(@"ApplicationDescription", this.Description);
        }

        using (var registeredApplications = Registry.CurrentUser.OpenSubKey(SoftwareRegisteredApplications, true))
            registeredApplications?.SetValue(this.UniqueName, this.CapabilityPath);
    }

    public void RegisterUriAssociations(UriAssociation[] associations)
    {
        using var capability = Registry.CurrentUser.OpenSubKey(this.CapabilityPath, true);
        if (capability == null) return;

        using var urlAssociations = capability.CreateSubKey(@"UrlAssociations");

        foreach (var association in associations)
            urlAssociations.SetValue(association.Protocol, association.ProgramId);
    }

    public void RegisterFileAssociations(FileAssociation[] associations)
    {
        using var capability = Registry.CurrentUser.OpenSubKey(this.CapabilityPath, true);
        if (capability == null) return;

        using var fileAssociations = capability.CreateSubKey(@"FileAssociations");

        foreach (var association in associations)
            fileAssociations.SetValue(association.Extension, association.ProgramId);
    }

    public void Uninstall()
    {
        using (var registeredApplications = Registry.CurrentUser.OpenSubKey(SoftwareRegisteredApplications, true))
            registeredApplications?.DeleteValue(this.UniqueName, throwOnMissingValue: false);

        Registry.CurrentUser.DeleteSubKeyTree(this.CapabilityPath, throwOnMissingSubKey: false);
    }
}
