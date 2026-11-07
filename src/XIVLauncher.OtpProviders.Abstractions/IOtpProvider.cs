using System.Threading;
using System.Threading.Tasks;

namespace XIVLauncher.OtpProviders
{
    public interface IOtpProvider
    {
        string Id { get; }
        string DisplayName { get; }
        Task<string?> GetOtpAsync(
        string username,
        CancellationToken cancellationToken = default);
    }
}
