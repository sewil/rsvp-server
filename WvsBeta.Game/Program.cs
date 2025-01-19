using System;
using System.IO;
using log4net;
using log4net.Config;
using WvsBeta.Common;
using WvsBeta.Logger;

namespace WvsBeta.Game
{
    public class Program
    {
        public static IMainForm MainForm { get; set; }

        public static string IMGFilename { get; set; }
        public static Logfile LogFile { get; private set; }

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Invalid argument length.");
                Environment.Exit(2);
                return;
            }

            IMGFilename = args[0];

            log4net.GlobalContext.Properties["ImgName"] = IMGFilename;
            
            Log4NetHelper.Init("logging-config-game.xml");
            
            UnhandledExceptionHandler.Set(args, IMGFilename, LogFile);

            MasterThread.Load(IMGFilename);
            LogFile = new Logfile(IMGFilename);
            
            var mainForm = new GameMainForm();
            MainForm = mainForm;
            mainForm.InitializeServer();
            mainForm.ReadInput();
        }
    }
}
