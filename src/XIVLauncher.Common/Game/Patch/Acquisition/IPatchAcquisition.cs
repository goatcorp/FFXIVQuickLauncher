using System.IO;
using System.Threading.Tasks;

namespace XIVLauncher.Common.Game.Patch.Acquisition;

public interface IPatchAcquisition
{
    Task StartIfNeededAsync(long speedLimitBps);
    PatchAcquisitionTask MakeTask(string url, FileInfo outFile);
    Task SetGlobalSpeedLimitAsync(long speedLimitBps);
}
