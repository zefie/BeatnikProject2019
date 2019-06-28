using System;

namespace ZefieLib
{
    public class Math
    {
        /// <summary>
        /// Calculates what percentage (<paramref name="value"/>) is of (<paramref name="max"/>)
        /// </summary>
        /// <param name="value"></param>
        /// <param name="max"></param>
        /// <returns>Percentage</returns>
        public static double CalcPercent(double value, double max)
        {
            return ((value / max) * 100);
        }
        /// <summary>
        /// Calculates what (<paramref name="value"/>) is (<paramref name="percent"/>) percent of
        /// </summary>
        /// <param name="value"></param>
        /// <param name="percent"></param>
        /// <returns>The full value that (<paramref name="value"/>) is (<paramref name="percent"/>) percent of</returns>
        public static double CalcPercentOf(double value, double percent)
        {
            return ((value / 100) * percent);
        }
        /// <summary>
        /// Generates a random number
        /// </summary>
        /// <param name="max">Highest number to generate</param>
        /// <returns>A random number</returns>
        public static int Random(int max)
        {
            Random rand = new Random(Cryptography.GenerateCryptoNumber());
            return rand.Next(max);
        }
        /// <summary>
        /// Generates a random number
        /// </summary>
        /// <param name="min">Lowest number to generate</param>
        /// <param name="max">Highest number to generate</param>
        /// <returns>A random number</returns>
        public static int Random(int min, int max)
        {
            if (min > max) throw new ArgumentOutOfRangeException("min should not be greater than max");
            if (min == max) return min;
            Random rand = new Random(new Random().Next(min, max));
            return rand.Next(min, max);
        }
        /// <summary>
        /// Calculates user-friendly interpretation of a file size
        /// </summary>
        /// <param name="value">File size in bytes</param>
        /// <returns>User-friendly interpretation of the file size</returns>
        public static string CalcBytes(Int64 value, int decimals = 2)
        {
            string[] SizeSuffixes = { "b", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };
            if (value < 0) { return "-" + CalcBytes(-value); }
            if (value == 0) { return "0 b"; }

            int mag = (int)System.Math.Log(value, 1024);
            decimal adjustedSize = System.Math.Round((decimal)value / (1L << (mag * 10)), decimals);
            
            return string.Format("{0:n1}{1}", adjustedSize, SizeSuffixes[mag]);
        }
    }
}
