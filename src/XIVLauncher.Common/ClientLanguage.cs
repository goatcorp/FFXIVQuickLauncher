namespace XIVLauncher.Common
{
    /// <summary>
    /// Game language types.
    /// </summary>
    public enum ClientLanguage
    {
        /// <summary>
        /// Japanese.
        /// </summary>
        Japanese,

        /// <summary>
        /// English.
        /// </summary>
        English,

        /// <summary>
        /// German.
        /// </summary>
        German,

        /// <summary>
        /// French.
        /// </summary>
        French,
    }

    /// <summary>
    /// Extension methods for the <see cref="ClientLanguage"/> enum.
    /// </summary>
    public static class ClientLanguageExtensions
    {
        /// <summary>
        /// Gets the ISO 639-1 language code.
        /// </summary>
        /// <param name="language">Client language.</param>
        /// <returns>An ISO 639-1 compliant language code.</returns>
        public static string GetLangCode(this ClientLanguage language)
        {
            switch (language)
            {
                case ClientLanguage.Japanese:
                    return "ja";

                case ClientLanguage.English when Util.IsRegionNorthAmerica():
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
    }
}
