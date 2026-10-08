using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace XIVLauncher.OtpProviders
{
    // handles OTP provider loading and execution. 
    // all failures are logged and return null so xl can fall back to manual otp entry
    public sealed class OtpProviderManager
    {
        private readonly IReadOnlyList<IOtpProvider> providers;

        public OtpProviderManager(IEnumerable<IOtpProvider> providers)
        // Sanitize input, never null out the list
        {
            this.providers = providers?.ToList() ?? new List<IOtpProvider>();
        }

        public IReadOnlyList<IOtpProvider> Providers => providers;

        public IOtpProvider GetProvider(string providerId)
        {
            if (string.IsNullOrWhiteSpace(providerId))
                return null;

            return providers.FirstOrDefault(provider => string.Equals(provider.Id, providerId, StringComparison.OrdinalIgnoreCase));
        }

        public async Task<string?> GetOtpAsync(
            string providerId,
            string username,
            CancellationToken cancellationToken = default)
        {
            var provider = GetProvider(providerId);

            if (provider == null)
            {
                Log.Warning("OTP provider {ProviderId} was not found.", providerId);
                return null;
            }

            try
            {
                var otp = await provider.GetOtpAsync(username, cancellationToken).ConfigureAwait(false);

                if (otp == null)
                    return null;

                if (!otp.All(char.IsDigit))
                {
                    Log.Warning("OTP provider {ProviderId} returned an invalid OTP.", provider.Id);
                    return null;
                }
                return otp;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "OTP provider {ProviderId} failed.", provider.Id);
                return null;
            }
        }
    }
}
