using System;

namespace XIVLauncher.Common
{
    public class SeVersion : IComparable
    {
        public uint Year { get; set; }
        public uint Month { get; set; }
        public uint Day { get; set; }
        public uint Revision { get; set; }
        public uint Part { get; set; }

        public static SeVersion Parse(string input)
        {
            var parts = input.Split('.');
            return new SeVersion
            {
                Year = uint.Parse(parts[0]),
                Month = uint.Parse(parts[1]),
                Day = uint.Parse(parts[2]),
                Revision = uint.Parse(parts[3]),
                Part = uint.Parse(parts[4]),
            };
        }

        public override string ToString() => $"{Year:0000}.{Month:00}.{Day:00}.{Revision:0000}.{Part:0000}";

        public int CompareTo(object obj)
        {
            var other = obj as SeVersion;
            if (other == null)
                return 1;

            if (Year > other.Year)
                return 1;

            if (Year < other.Year)
                return -1;

            if (Month > other.Month)
                return 1;

            if (Month < other.Month)
                return -1;

            if (Day > other.Day)
                return 1;

            if (Day < other.Day)
                return -1;

            if (Revision > other.Revision)
                return 1;

            if (Revision < other.Revision)
                return -1;

            if (Part > other.Part)
                return 1;

            if (Part < other.Part)
                return -1;

            return 0;
        }

        public static bool operator <(SeVersion x, SeVersion y) => x.CompareTo(y) < 0;
        public static bool operator >(SeVersion x, SeVersion y) => x.CompareTo(y) > 0;
        public static bool operator <=(SeVersion x, SeVersion y) => x.CompareTo(y) <= 0;
        public static bool operator >=(SeVersion x, SeVersion y) => x.CompareTo(y) >= 0;

        public static bool operator ==(SeVersion x, SeVersion y)
        {
            if (x is null)
                return y is null;

            return x.CompareTo(y) == 0;
        }

        public static bool operator !=(SeVersion x, SeVersion y)
        {
            if (x is null)
                return y != null;

            return x.CompareTo(y) != 0;
        }
    }
}