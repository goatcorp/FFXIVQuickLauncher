using System;

namespace XIVLauncher.Common.Util;

public static class TimeSpanExtensions
{
    public static string ToFriendlyString(this TimeSpan span)
    {
        // https://gist.github.com/Rychu-Pawel/fefb89e21b764e97e4993ff517ff0129

        if (span.TotalSeconds < 5)
            return "just now";

        return span switch
        {
            { TotalDays: > 1 } => $"{span.Days:0} day{(span.Days == 1 ? string.Empty : "s")} ago",
            { TotalHours: > 1 } => $"{span.Hours:0} hour{(span.Hours == 1 ? string.Empty : "s")} ago",
            { TotalMinutes: > 1 } => $"{span.Minutes:0} minute{(span.Minutes == 1 ? string.Empty : "s")} ago",
            _ => $"{span.Seconds:0} second{(span.Seconds == 1 ? string.Empty : "s")} ago"
        };
    }
}
