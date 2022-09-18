using System.Text;
using Newtonsoft.Json;
using Serilog;
using XIVLauncher.Common;
using XIVLauncher.Common.Dalamud;
using XIVLauncher.Common.Game;
using XIVLauncher.Common.Util;

namespace XIVLauncher.Core.Support
{
    /// <summary>
    /// Class responsible for printing troubleshooting information to the log.
    /// </summary>
    public static class Troubleshooting
    {
        /// <summary>
        /// Gets the most recent exception to occur.
        /// </summary>
        public static Exception? LastException { get; private set; }

        /// <summary>
        /// Log the last exception in a parseable format to serilog.
        /// </summary>
        /// <param name="exception">The exception to log.</param>
        /// <param name="context">Additional context.</param>
        public static void LogException(Exception exception, string context)
        {
            LastException = exception;

            try
            {
                var fixedContext = context?.Split(new []{'\r', '\n'}, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();

                var payload = new ExceptionPayload
                {
                    Context = fixedContext,
                    When = DateTime.Now,
                    Info = exception.ToString(),
                };

                var encodedPayload = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(payload)));
                Log.Information($"LASTEXCEPTION:{encodedPayload}");
            }
            catch (Exception)
            {
                Log.Error("Could not print exception");
            }
        }

        /// <summary>
        /// Log troubleshooting information in a parseable format to Serilog.
        /// </summary>
        internal static void LogTroubleshooting()
        {
            try
            {
                var encodedPayload = Convert.ToBase64String(Encoding.UTF8.GetBytes(GetTroubleshootingJson()));
                Log.Information($"TROUBLESHXLTING:{encodedPayload}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Could not print troubleshooting");
            }
        }

        internal static string GetTroubleshootingJson()
        {
            
            var gamePath = Program.Config.GamePath;

            var integrity = TroubleshootingPayload.IndexIntegrityResult.Success;

            try
            {
                if (!gamePath.Exists || !gamePath.GetDirectories().Any(x => x.Name == "game"))
                {
                    integrity = TroubleshootingPayload.IndexIntegrityResult.NoGame;
                }
                else
                {
                    var result = IntegrityCheck.CompareIntegrityAsync(null, gamePath, true).Result;

                    integrity = result.compareResult switch
                    {
                        IntegrityCheck.CompareResult.ReferenceFetchFailure => TroubleshootingPayload.IndexIntegrityResult.ReferenceFetchFailure,
                        IntegrityCheck.CompareResult.ReferenceNotFound => TroubleshootingPayload.IndexIntegrityResult.ReferenceNotFound,
                        IntegrityCheck.CompareResult.Invalid => TroubleshootingPayload.IndexIntegrityResult.Failed,
                        _ => integrity
                    };
                }
            }
            catch (Exception)
            {
                integrity = TroubleshootingPayload.IndexIntegrityResult.Exception;
            }
            
            var ffxivVer = Repository.Ffxiv.GetVer(gamePath);
            var ffxivVerBck = Repository.Ffxiv.GetVer(gamePath, true);
            var ex1Ver = Repository.Ex1.GetVer(gamePath);
            var ex1VerBck = Repository.Ex1.GetVer(gamePath, true);
            var ex2Ver = Repository.Ex2.GetVer(gamePath);
            var ex2VerBck = Repository.Ex2.GetVer(gamePath, true);
            var ex3Ver = Repository.Ex3.GetVer(gamePath);
            var ex3VerBck = Repository.Ex3.GetVer(gamePath, true);
            var ex4Ver = Repository.Ex4.GetVer(gamePath);
            var ex4VerBck = Repository.Ex4.GetVer(gamePath, true);

            var payload = new TroubleshootingPayload
            {
                When = DateTime.Now,
                IsDx11 = Program.Config.IsDx11.GetValueOrDefault(),
                IsAutoLogin = Program.Config.IsAutologin.GetValueOrDefault(),
                IsUidCache = Program.Config.IsUidCacheEnabled.GetValueOrDefault(),
                DalamudEnabled = Program.Config.DalamudEnabled.GetValueOrDefault(),
                DalamudLoadMethod = Program.Config.DalamudLoadMethod.GetValueOrDefault(),
                DalamudInjectionDelay = Program.Config.DalamudLoadDelay,
                EncryptArguments = Program.Config.IsEncryptArgs.GetValueOrDefault(true),
                LauncherVersion = AppUtil.GetAssemblyVersion(),
                LauncherHash = AppUtil.GetGitHash() ?? "<unavailable>",
                Official = AppUtil.GetBuildOrigin() == "goatcorp/FFXIVQuickLauncher",
                DpiAwareness = Program.Config.DpiAwareness.GetValueOrDefault(),
                Platform = PlatformHelpers.GetPlatform(),

                ObservedGameVersion = ffxivVer,
                ObservedEx1Version = ex1Ver,
                ObservedEx2Version = ex2Ver,
                ObservedEx3Version = ex3Ver,
                ObservedEx4Version = ex4Ver,

                BckMatch = ffxivVer == ffxivVerBck && ex1Ver == ex1VerBck && ex2Ver == ex2VerBck &&
                           ex3Ver == ex3VerBck && ex4Ver == ex4VerBck,

                IndexIntegrity = integrity
            };

            return JsonConvert.SerializeObject(payload);
        }

        private class ExceptionPayload
        {
            public DateTime When { get; set; }

            public string Info { get; set; }

            public string Context { get; set; }
        }

        private class TroubleshootingPayload
        {
            public DateTime When { get; set; }

            public bool IsDx11 { get; set; }

            public bool IsAutoLogin { get; set; }

            public bool IsUidCache { get; set; }

            public bool DalamudEnabled { get; set; }

            public DalamudLoadMethod DalamudLoadMethod { get; set; }

            public decimal DalamudInjectionDelay { get; set; }

            public bool SteamIntegration { get; set; }

            public bool EncryptArguments { get; set; }

            public string LauncherVersion { get; set; }

            public string LauncherHash { get; set; }

            public bool Official { get; set; }

            public DpiAwareness DpiAwareness { get; set; }

            public Platform Platform { get; set; }

            public string ObservedGameVersion { get; set; }

            public string ObservedEx1Version { get; set; }
            public string ObservedEx2Version { get; set; }
            public string ObservedEx3Version { get; set; }
            public string ObservedEx4Version { get; set; }

            public bool BckMatch { get; set; }

            public enum IndexIntegrityResult
            {
                Failed,
                Exception,
                NoGame,
                ReferenceNotFound,
                ReferenceFetchFailure,
                Success,
            }

            public IndexIntegrityResult IndexIntegrity { get; set; }
        }
    }
}