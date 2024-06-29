using System;
using System.Linq;
using System.Net.Http.Headers;

namespace XIVLauncher.Common.Util;

public static class ApiHelpers
{
    public static long GetUnixMillis()
    {
        return (long)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds;
    }

    public static string BytesToString(double byteCount) => BytesToString(Convert.ToInt64(Math.Floor(byteCount)));

    public static string BytesToString(long byteCount)
    {
        string[] suf = { "B", "KB", "MB", "GB", "TB", "PB", "EB" }; //Longs run out around EB

        if (byteCount == 0)
            return "0" + suf[0];

        var bytes = Math.Abs(byteCount);
        var place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
        var num = Math.Round(bytes / Math.Pow(1024, place), 1);
        return $"{(Math.Sign(byteCount) * num):#0.0}{suf[place]}";
    }

    public static string GetTimeLeft(TimeSpan span, string[] locs)
    {
        if (span.TotalSeconds < 1)
            return "";

        var seconds = (long)span.TotalSeconds;
        var minutes = seconds / 60;
        var hours = minutes / 60;
        var days = hours / 24;

        if (days > 0)
            return string.Format(locs[0], days, hours % 24, minutes % 60, seconds % 60);
        if (hours > 0)
            return string.Format(locs[1], hours, minutes % 60, seconds % 60);

        return minutes > 0 ? string.Format(locs[2], minutes, seconds % 60) : string.Format(locs[3], seconds % 60);
    }

    public static string GenerateAcceptLanguage(int asdf = 0)
    {
        var codes = new string[] { "de-DE", "en-US", "ja" };
        var codesMany = new string[] { "de-DE", "en-US,en", "en-GB,en", "fr-BE,fr", "ja", "fr-FR,fr", "fr-CH,fr" };
        var rng = new Random(asdf);

        var many = rng.Next(10) < 3;

        if (many)
        {
            var howMany = rng.Next(2, 4);
            var deck = codesMany.OrderBy((x) => rng.Next()).Take(howMany).ToArray();

            var hdr = string.Empty;

            for (int i = 0; i < deck.Count(); i++)
            {
                hdr += deck.ElementAt(i) + $";q=0.{10 - (i + 1)}";

                if (i != deck.Length - 1)
                    hdr += ";";
            }

            return hdr;
        }

        return codes[rng.Next(0, codes.Length)];
    }

    public static void AddWithoutValidation(this HttpHeaders headers, string key, string value)
    {
        var res = headers.TryAddWithoutValidation(key, value);

        if (!res)
            throw new InvalidOperationException($"Could not add header - {key}: {value}");
    }

    /// <summary>
    /// Gets an attribute on an enum.
    /// </summary>
    /// <typeparam name="TAttribute">The type of attribute to get.</typeparam>
    /// <param name="value">The enum value that has an attached attribute.</param>
    /// <returns>The attached attribute, if any.</returns>
    public static TAttribute? GetAttribute<TAttribute>(this Enum value)
        where TAttribute : Attribute
    {
        var type = value.GetType();
        var memInfo = type.GetMember(value.ToString());
        var attributes = memInfo[0].GetCustomAttributes(typeof(TAttribute), false);
        return (attributes.Length > 0) ? (TAttribute)attributes[0] : null;
    }
}
