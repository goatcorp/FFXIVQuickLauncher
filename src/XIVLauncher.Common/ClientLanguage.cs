using XIVLauncher.Common.Util;

namespace XIVLauncher.Common
{
    public enum ClientLanguage
    {
        Japanese,
        English,
        German,
        French
    }

    public static class ClientLanguageExtensions
    {
        public static string GetLangCode(this ClientLanguage language, bool forceNa = false)
        {
            switch (language)
            {
                case ClientLanguage.Japanese:
                    return "ja";

                case ClientLanguage.English when GameHelpers.IsRegionNorthAmerica() || forceNa:
                    return "en-us";

                case ClientLanguage.English:
                    return "en-gb";

                case ClientLanguage.German:
                    return "de";

                case ClientLanguage.French:
                    return "fr";

                default:
                    return "en-gb";
            }
        }

        public static string GetLangCodeLodestone(this ClientLanguage language, bool forceNa = false)
        {
            switch (language)
            {
                case ClientLanguage.Japanese:
                    return "jp";

                case ClientLanguage.English when GameHelpers.IsRegionNorthAmerica() || forceNa:
                    return "na";

                case ClientLanguage.English:
                    return "eu";

                case ClientLanguage.German:
                    return "de";

                case ClientLanguage.French:
                    return "fr";

                default:
                    return "eu";
            }
        }
    }
}
