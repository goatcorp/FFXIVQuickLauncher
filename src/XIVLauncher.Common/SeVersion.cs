using System;

namespace XIVLauncher.Common
{
    /// <summary>
    /// This represents an SE version string.
    /// </summary>
    public class SeVersion : IComparable
    {
        /// <summary>
        /// Gets or sets the year.
        /// </summary>
        public uint Year { get; set; }

        /// <summary>
        /// Gets or sets the month.
        /// </summary>
        public uint Month { get; set; }

        /// <summary>
        /// Gets or sets the day.
        /// </summary>
        public uint Day { get; set; }

        /// <summary>
        /// Gets or sets the revision.
        /// </summary>
        public uint Revision { get; set; }

        /// <summary>
        /// Gets or sets the part.
        /// </summary>
        public uint Part { get; set; }

        /// <summary>
        /// Parse a string into an <see cref="SeVersion"/>.
        /// </summary>
        /// <param name="input">Input string.</param>
        /// <returns>A parsed SeVersion.</returns>
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

        /// <inheritdoc/>
        public override string ToString() => $"{Year:0000}.{Month:00}.{Day:00}.{Revision:0000}.{Part:0000}";

        /// <inheritdoc/>
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
    }
}
