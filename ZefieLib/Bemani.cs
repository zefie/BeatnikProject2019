namespace ZefieLib
{
    public class Bemani
    {
        /// <summary>
        /// Generates a PSun compatible eAmuse card code
        /// </summary>
        public static string EAmuseCardGen()
        {
            return "E004" + Strings.GenerateHexString(12);
        }
    }
}
