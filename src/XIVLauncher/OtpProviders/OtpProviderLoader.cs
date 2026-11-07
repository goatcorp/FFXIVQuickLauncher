using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Serilog;


namespace XIVLauncher.OtpProviders
{
    // plugin loader for OTP providers.
    // loads DLLs, validates interfaces, and rejects anything that looks suspicious.
    // if a plugin crashes, is malformed, or tries to load twice, we skip it and log. 
    // nothing crashes XL.
    public static class OtpProviderLoader
    {
        public static IReadOnlyList<IOtpProvider> LoadProviders(string providerDirectory)
        {
            var providers = new List<IOtpProvider>();

            if (string.IsNullOrWhiteSpace(providerDirectory) || !Directory.Exists(providerDirectory))
                return providers;
            // scan and attempt load. Bad DLLs are caught and ignored by the handler below.
            foreach (var dllPath in Directory.EnumerateFiles(providerDirectory, "*.dll"))
                LoadProvidersFromAssembly(dllPath, providers);
            // sort for consistent output.
            return providers
                .OrderBy(provider => provider.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static void LoadProvidersFromAssembly(string dllPath, ICollection<IOtpProvider> providers)
        {
            try
            {
                var fullPath = Path.GetFullPath(dllPath);
                var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(fullPath);
                foreach (var providerType in GetProviderTypes(assembly)) TryAddProvider(providerType, assembly, providers);
            }

            catch (Exception ex)
            {
                Log.Warning(
                    ex, "Could not load OTP provider assembly {ProviderAssembly}",
                    Path.GetFileName(dllPath));
            }
        }

        private static IEnumerable<Type> GetProviderTypes(Assembly assembly)
        {
            // filter for valid IOtpProvider implementations only
            return assembly.GetTypes()
                .Where(type => typeof(IOtpProvider).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract);
        }

        private static void TryAddProvider(Type providerType, Assembly assembly, ICollection<IOtpProvider> providers)
        {
            IOtpProvider provider;
            try
            {
                var instance = Activator.CreateInstance(providerType);
                if (instance is not IOtpProvider) return;

                provider = (IOtpProvider)instance;
            }
            catch (Exception ex)
            {
                // constructor crashed? skip it.
                Log.Warning(ex, "Could not create OTP provider from type {ProviderType}", providerType.FullName);
                return;
            }

            if (providers.Any(existing => string.Equals(existing.Id, provider.Id, StringComparison.OrdinalIgnoreCase)))
            {
                Log.Warning("Ignoring duplicate OTP provider {ProviderId} from {ProviderAssembly}", provider.Id, assembly.GetName().Name);
                return;
            }

            providers.Add(provider);
            Log.Information("Loaded OTP provider {ProviderName} ({ProviderId})", provider.DisplayName, provider.Id);
        }
    }
}
