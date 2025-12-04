using System;
using System.Text.RegularExpressions;
using Serilog;

namespace XIVLauncher.Common.Game.Exceptions;

[Serializable]
public class OauthLoginException : Exception
{
    private static Regex errorMessageRegex =
        new(@"window.external.user\(""login=auth,ng,err,(?<errorMessage>.*)\""\);", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public string? OauthErrorMessage { get; private set; }

    public OauthLoginException(string document)
        : base(GetErrorMessage(document) ?? "Unknown error")
    {
        this.OauthErrorMessage = GetErrorMessage(document);
    }

    private static string? GetErrorMessage(string document)
    {
        var matches = errorMessageRegex.Matches(document);

        if (matches.Count == 1)
        {
            return matches[0].Groups["errorMessage"].Value;
        }

        // If regex doesn't match, try to extract error from common patterns
        Log.Error("Could not get login error\n{Doc}", document);
        
        // Try to extract from "Login failed: message" or "server returned error: message" patterns
        var patterns = new[] { "Login failed:", "server returned error:", "Error:", "Failed:" };
        foreach (var pattern in patterns)
        {
            var index = document.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
            {
                var startIndex = index + pattern.Length;
                var message = document.Substring(startIndex).Trim();
                // Extract until newline or end of string
                var endIndex = message.IndexOfAny(new[] { '\r', '\n' });
                if (endIndex > 0)
                {
                    message = message.Substring(0, endIndex).Trim();
                }
                if (!string.IsNullOrEmpty(message))
                {
                    return message;
                }
            }
        }
        
        return null;
    }
}