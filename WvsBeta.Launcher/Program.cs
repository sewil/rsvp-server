namespace WvsBeta.Launcher
{
    internal static class Program
    {
        public static readonly string InstallationPath = Environment.CurrentDirectory;
        public static string DataSvr => Path.Join(InstallationPath, "..", "DataSvr");

        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            MasterThread.Load("Launcher");

            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();
            Application.Run(new Form1());
        }
    }
}