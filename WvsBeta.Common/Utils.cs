using System.Globalization;

namespace WvsBeta.Common
{
    public class Utils
    {
        public static long ConvertNameToID(string pName)
        {
            if (pName[pName.Length - 1] == 'g')
            {
                pName = pName.Remove(pName.Length - 4);
            }

            // Trim all zeroes from start
            while (pName.Length > 1 && pName[0] == '0')
            {
                pName = pName.Substring(1);
            }

            return long.Parse(pName, NumberStyles.Integer);
        }
        
    }
}
