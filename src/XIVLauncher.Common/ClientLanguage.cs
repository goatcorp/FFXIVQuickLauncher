using XIVLauncher.Common.Util;

namespace XIVLauncher.Common
{
    public enum ClientLanguage
    {
        Japanese,
        English,
        German,
        French,
        TraditionalChinese,
    }

    public static class ClientLanguageExtensions
    {
        public static string GetLangCode(this ClientLanguage language, bool forceNa = false)
        {
            return language switch
            {
                ClientLanguage.Japanese => "ja",
                ClientLanguage.English when GameHelpers.IsRegionNorthAmerica() || forceNa => "en-us",
                ClientLanguage.English => "en-gb",
                ClientLanguage.German => "de",
                ClientLanguage.French => "fr",
                ClientLanguage.TraditionalChinese => "zh",
                _ => "en-gb",
            };
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
