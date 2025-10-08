using WvsBeta.Logger;
using WzTools.FileSystem;

namespace WvsBeta.DataValidator
{
    class Program
    {
        public static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Usage: WvsBeta.DataValidator.exe <path to data directory> [map|rewards]");
                Environment.Exit(1);
            }

            var fileSystem = new WzFileSystem();
            fileSystem.Init(args[0]);

            if (args.Length < 2 || args[1] == "map")
            {
                MapValidator.Validate(fileSystem);
            }
            if (args.Length < 2 || args[1] == "rewards")
            {
                RewardsValidator.Validate(fileSystem);
            }
        }
    }
}
