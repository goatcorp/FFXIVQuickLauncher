using Serilog;

namespace XIVLauncher.Core;

public static class UpdateCheck
{
    private const string UPDATE_URL = "https://raw.githubusercontent.com/goatcorp/xlcore-distrib/main/version.txt";

    public static async Task<VersionCheckResult> CheckForUpdate()
    {
        try
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(5);

            var response = await client.GetStringAsync(UPDATE_URL).ConfigureAwait(false);
            var remoteVersion = Version.Parse(response);

            var localVersion = Version.Parse(AppUtil.GetAssemblyVersion());

            return new VersionCheckResult
            {
                Success = true,
                WantVersion = response,
                NeedUpdate = remoteVersion > localVersion,
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Could not check version");

            return new VersionCheckResult
            {
                Success = false,
            };
        }
    }

    public class VersionCheckResult
    {
        public bool Success { get; set; }
        public bool NeedUpdate { get; init; }
        public string? WantVersion { get; init; }
    }
}