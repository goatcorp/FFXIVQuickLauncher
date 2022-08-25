using System.Collections.Generic;

namespace XIVLauncher.Common
{
    public enum LauncherLanguage
    {
        Japanese,
        English,
        German,
        French,
        Italian,
        Spanish,
        Portuguese,
        Korean,
        Norwegian,
        Russian,
        SimplifiedChinese,
        TraditionalChinese,
        Swedish,
    }

    public static class LauncherLanguageExtensions
    {
        private static Dictionary<LauncherLanguage, string> GetLangCodes()
        {
            // MUST match LauncherLanguage enum
            return new Dictionary<LauncherLanguage, string>
            {
                { LauncherLanguage.Japanese, "ja" },
                { LauncherLanguage.English, "en" },
                { LauncherLanguage.German, "de" },
                { LauncherLanguage.French, "fr" },
                { LauncherLanguage.Italian, "it" },
                { LauncherLanguage.Spanish, "es" },
                { LauncherLanguage.Portuguese, "pt" },
                { LauncherLanguage.Korean, "ko" },
                { LauncherLanguage.Norwegian, "no" },
                { LauncherLanguage.Russian, "ru" },
                { LauncherLanguage.SimplifiedChinese, "zh" },
                { LauncherLanguage.TraditionalChinese, "tw" },
                { LauncherLanguage.Swedish, "sv" },
            };
        }

        public static string GetLocalizationCode(this LauncherLanguage? language)
        {
            return GetLangCodes()[language ?? LauncherLanguage.English]; // Default localization language
        }

        public static bool IsDefault(this LauncherLanguage? language)
        {
            return language == null || language == LauncherLanguage.English;
        }

        public static LauncherLanguage GetLangFromTwoLetterIso(this LauncherLanguage? language, string code)
        {
            foreach (var langCode in GetLangCodes())
            {
                if (langCode.Value == code)
                {
                    return langCode.Key;
                }
            }

            return LauncherLanguage.English; // Default language
        }
    }
}