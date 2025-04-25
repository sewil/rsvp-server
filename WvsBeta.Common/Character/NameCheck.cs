using System.Collections.Generic;
using System.Linq;
using WzTools.FileSystem;

namespace WvsBeta.Common.Character
{
    public static class NameCheck
    {
        private static IList<string> _forbiddenName;
        public enum Result
        {
            OK,
            InvalidLength,
            InvalidCharacter,
            Forbidden,
        }

        public static Result Check(string pName)
        {
            if (pName.Length < 4 || pName.Length > 12)
                return Result.InvalidLength;

            if (pName.Any(x =>
                {
                    if (x >= 'a' && x <= 'z') return false;
                    if (x >= 'A' && x <= 'Z') return false;
                    if (x >= '0' && x <= '9') return false;
                    return true;
                }))
                return Result.InvalidCharacter;

            if (_forbiddenName.Exists(pName.ToLower().Contains)) 
                return Result.Forbidden;

            return Result.OK;
        }


        public static void LoadForbiddenName(WzFileSystem fileSystem)
        {
            _forbiddenName = fileSystem.GetProperty("Etc", "ForbiddenName.img").Children.OfType<string>()
                .Select(i => i.ToLower())
                .ToList();
        }
    }
}
