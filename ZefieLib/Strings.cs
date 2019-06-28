using System;
using System.Linq;
using System.Text;

namespace ZefieLib
{
    public class Strings
    {
        /// <summary>
        /// Generates a hexadecimal string
        /// </summary>
        /// <param name="length">Number of bytes</param>
        /// <returns>A hexadecimal string</returns>
        public static string GenerateHexString(int length)
        {
            return Cryptography.GenerateHash(length * 8);
        }
        /// <summary>
        /// Generates a random string
        /// </summary>
        /// <param name="length">Number of characters</param>
        /// <param name="chars">Characters to use in generation</param>
        /// <returns>A random string of characters</returns>
        public static string GenerateString(int length, string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789")
        {
            var random = new Random(Cryptography.GenerateCryptoNumber());
            var result = new string(
                Enumerable.Repeat(chars, length)
                          .Select(s => s[ZefieLib.Math.Random(s.Length)])
                          .ToArray());
            return result;
        }
        /// <summary>
        /// Generates a random string
        /// </summary>
        /// <param name="length">Number of characters</param>
        /// <param name="append">Characters to append to the default set of characters</param>
        /// <returns>A random string of characters</returns>
        public static string GenerateString(int length, char[] append)
        {
            return GenerateString(length, "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789" + new String(append));
        }
    }
}
