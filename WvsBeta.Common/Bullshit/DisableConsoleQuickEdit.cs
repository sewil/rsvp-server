using System;
using System.Runtime.InteropServices;


namespace WvsBeta.Common.Bullshit
{
    // https://stackoverflow.com/a/36720802

    static class DisableConsoleQuickEdit
    {
        const uint ENABLE_QUICK_EDIT = 0x0040;

        // STD_INPUT_HANDLE (DWORD): -10 is the standard input device.
        const int STD_INPUT_HANDLE = -10;

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

        internal static bool Go()
        {
            if (!OperatingSystem.IsWindows())
            {
                Console.WriteLine("Skipping QuickEdit disabling, this OS doesn't have that!");
                return true;
            }

            IntPtr consoleHandle = GetStdHandle(STD_INPUT_HANDLE);

            // get current console mode
            uint consoleMode;
            if (!GetConsoleMode(consoleHandle, out consoleMode))
            {
                // ERROR: Unable to get console mode.
                Console.WriteLine("Unable to get console mode. Last error: {0}", Marshal.GetLastPInvokeError());
                return false;
            }

            // Clear the quick edit bit in the mode flags
            consoleMode &= ~ENABLE_QUICK_EDIT;

            // set the new mode
            if (!SetConsoleMode(consoleHandle, consoleMode))
            {
                // ERROR: Unable to set console mode
                Console.WriteLine("Unable to set console mode. Last error: {0}", Marshal.GetLastPInvokeError());
                return false;
            }

            Console.WriteLine("QuickEdit disabled.");
            return true;
        }
    }
}