using System;
using WvsBeta.Common;
using WvsBeta.Logger;


namespace WvsBeta.Shop
{
    class Program
    {
        public static ShopMainForm MainForm { get; set; }

        public static string IMGFilename { get; set; }
        public static Common.Logfile LogFile { get; private set; }

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.WriteLine("Invalid argument length.");
                Environment.Exit(2);
                return;
            }

            IMGFilename = args[0];
            log4net.GlobalContext.Properties["ImgName"] = IMGFilename;

            Log4NetHelper.Init("logging-config-shop.xml");

            LogFile = new Common.Logfile(IMGFilename);


            UnhandledExceptionHandler.Set(args, IMGFilename, LogFile);
            MasterThread.Load(IMGFilename);

            MainForm = new ShopMainForm();
            MainForm.InitializeServer();
            MainForm.ReadInput();

        }
    }
}
