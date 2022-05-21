namespace XIVLauncher.Common.Dalamud
{
    public enum DalamudLoadMethod
    {
        [SettingsDescription("Entrypoint", "dummy")]
        EntryPoint,

        [SettingsDescription("DLL Injection", "dummy")]
        DllInject,

        [SettingsDescription("ACL-only fix", "dummy")]
        ACLonly,
    };
}