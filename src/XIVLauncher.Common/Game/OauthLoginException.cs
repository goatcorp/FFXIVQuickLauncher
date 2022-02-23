using System;
using System.Text.RegularExpressions;
using Serilog;

namespace XIVLauncher.Common.Game  
{
    [Serializable]
    public class OauthLoginException : Exception
    {
        private static Regex errorMessageRegex =
            new(@"window.external.user\(""login=auth,ng,err,(?<errorMessage>.*)\""\);", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public OauthLoginException(string document) : base(GetErrorMessage(document))
        {
            // ignored
        }

        private static string GetErrorMessage(string document)
        {
            var matches = errorMessageRegex.Matches(document);

            if (matches.Count is 0 or > 1)
            {
                Log.Error("Could not get login error\n" + document);
                throw new Exception("Could not get login error"); // TODO(goat): hook up
                // return Loc.Localize("LoginGenericError", "Could not log into your SE account.\nPlease check your username and password.");
            }

            return matches[0].Groups["errorMessage"].Value;
        }
    }
}
