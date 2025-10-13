using System;

namespace XIVLauncher.Common.Util;

public class MathHelpers
{
    public const int BYTES_TO_MB = 1048576;

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
}
