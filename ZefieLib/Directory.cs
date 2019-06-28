using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;


namespace ZefieLib
{
    public static class Directory
    {
        /// <summary>
        /// List of supported file extensions
        /// </summary>
        public static List<string> supportedExtensions = new List<string>();

        /// <summary>
        /// Copies a directory's contents (and optionally its tree) to another directory
        /// </summary>
        /// <param name="sourceDirName">Source Directory</param>
        /// <param name="destDirName">Destionation Directory</param>
        /// <param name="copySubDirs">Recursive</param>
        public static void Copy(string sourceDirName, string destDirName, bool copySubDirs)
        {
            // Get the subdirectories for the specified directory.
            DirectoryInfo dir = new DirectoryInfo(sourceDirName);
            DirectoryInfo[] dirs = dir.GetDirectories();

            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException(
                    "Source directory does not exist or could not be found: "
                    + sourceDirName);
            }

            // If the destination directory doesn't exist, create it. 
            if (!System.IO.Directory.Exists(destDirName))
            {
                System.IO.Directory.CreateDirectory(destDirName);
            }

            // Get the files in the directory and copy them to the new location.
            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                string temppath = System.IO.Path.Combine(destDirName, file.Name);
                file.CopyTo(temppath, false);
            }

            // If copying subdirectories, copy them and their contents to new location. 
            if (copySubDirs)
            {
                foreach (DirectoryInfo subdir in dirs)
                {
                    string temppath = System.IO.Path.Combine(destDirName, subdir.Name);
                    Directory.Copy(subdir.FullName, temppath, copySubDirs);
                }
            }
        }

        /// <summary>
        /// Checks whether or not the filename matches extensions defined in supportedExtensions
        /// </summary>
        /// <param name="file">Filename to check against supportedExtensions</param>
        /// <returns>True if matches, false otherwise. If no supportedExtensions are defined, this will always return true.</returns>
        public static bool IsSupported(string file)
        {
            if (supportedExtensions.Count() > 0)
            {
                string r = "";
                for (int i = 0; i < supportedExtensions.Count(); i++)
                    r += supportedExtensions[i] + "|";

                r = r.TrimEnd('|');
                if (String.IsNullOrEmpty(file))
                    return false;
                Regex ser = new Regex(@"\.(" + r + ")", RegexOptions.IgnoreCase);
                return ser.IsMatch(System.IO.Path.GetExtension(file) ?? String.Empty);
            }
            return true;
        }

        /// <summary>
        /// An extension of System.IO.Directory.GetFiles, this offers a natural sorting algorithm, RegEx searching, and defining an array of supported extensions.
        /// </summary>
        /// <param name="path">Directory to scan for files</param>
        /// <param name="search_string">RegExp search string</param>
        /// <param name="sort">Use natural sorting</param>
        /// <param name="recursive">Recursively scan directories</param>
        /// <returns>Array of files that match the search pattern (if any), and Zefie.Directory.supportedExtensions</returns>
        public static string[] GetFiles(string path, string search_string = null, bool sort = true, bool recursive = false)
        {
            if (!System.IO.Directory.Exists(path))
                return null;

            
            if (recursive)
            {
                List<string> fsr = new List<string>();
                foreach (string dir in ZefieLib.Directory.GetDirectories(path, null, true, true))
                {
                    try { fsr.AddRange(GetFiles(dir, search_string, sort)); }
                    catch { };
                }
                return fsr.ToArray();
            }

            SearchOption so = SearchOption.TopDirectoryOnly;
            string[] fs = null;
            if (sort)
            {
                if (search_string != null)
                {
                    Regex r = new Regex("(?i)" + search_string.Replace("*", ".*") + "(?-i)", RegexOptions.IgnoreCase);
                    fs = System.IO.Directory.GetFiles(path, "*", so).Where(IsSupported).Where(a => r.Match(a).Success).OrderByAlphaNumeric().ToArray();
                }
                else
                    fs = System.IO.Directory.GetFiles(path, "*", so).Where(IsSupported).OrderByAlphaNumeric().ToArray();
            }
            else
                if (search_string != null)
                {
                    Regex r = new Regex("(?i)" + search_string.Replace("*", ".*") + "(?-i)", RegexOptions.IgnoreCase);
                    fs = System.IO.Directory.GetFiles(path, "*", so).Where(IsSupported).Where(a => r.Match(a).Success).ToArray();
                }
                else
                    fs = System.IO.Directory.GetFiles(path, "*", so).Where(IsSupported).ToArray();

            return fs;
        }
        /// <summary>
        /// An extension of System.IO.Directory.GetDirectories, this offers a natural sorting algorithm, and RegEx searching.
        /// </summary>
        /// <param name="path">Directory to scan for subdirectories</param>
        /// <param name="search_string">RegExp search string</param>
        /// <param name="sort">Use natural sorting</param>
        /// <param name="recursive">Recursively scan directories</param>
        /// <returns>Array of directories that match the search pattern (if any)</returns>
        public static string[] GetDirectories(string path, string search_string = null, bool sort = true, bool recursive = false)
        {
            if (!System.IO.Directory.Exists(path))
                return null;

            if (recursive)
            {
                List<string> dsr = new List<string>();
                foreach (string dir in ZefieLib.Directory.GetDirectories(path, null, false, false))
                {
                    dsr.Add(dir);
                    try { dsr.AddRange(GetDirectories(dir, search_string, sort, true)); }
                    catch { };
                }
                return dsr.ToArray();
            }

            SearchOption so = SearchOption.TopDirectoryOnly;
            string[] ds = null;
            if (sort)
            {
                if (search_string != null)
                {
                    Regex r = new Regex("(?i)" + search_string.Replace("*", ".*") + "(?-i)");
                    ds = System.IO.Directory.GetDirectories(path, "*", so).Where(a => r.Match(a).Success).OrderByAlphaNumeric().ToArray();
                }
                else
                    ds = System.IO.Directory.GetDirectories(path, "*", so).OrderByAlphaNumeric().ToArray();
            }
            else
                if (search_string != null)
                {
                    Regex r = new Regex("(?i)" + search_string.Replace("*", ".*") + "(?-i)");
                    ds = System.IO.Directory.GetDirectories(path, "*", so).Where(a => r.Match(a).Success).ToArray();
                }
                else
                    ds = System.IO.Directory.GetDirectories(path, "*", so).ToArray();

            return ds;
        }

        internal static IEnumerable<string> OrderByAlphaNumeric(this IEnumerable<string> source)
        {
            return OrderByAlphaNumeric(source, t => t);
        }

        internal static IEnumerable<T> OrderByAlphaNumeric<T>(this IEnumerable<T> source, Func<T, string> selector)
        {
            int max = source
                .SelectMany(i => Regex.Matches(selector(i), @"\d+").Cast<Match>().Select(m => (int?)m.Value.Length))
                .Max() ?? 0;

            return source.OrderBy(i => Regex.Replace(selector(i), @"\d+", m => m.Value.PadLeft(max, '0')));
        }
    }
}
