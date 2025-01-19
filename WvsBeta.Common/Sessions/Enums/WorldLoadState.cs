using System;
using System.Collections.Generic;
using System.Text;

namespace WvsBeta.Common
{
    public enum WorldLoadState
    {
        OK = 0,
        // World is quite loaded, expect issues
        Warning = 1,
        // World is full
        Full = 2
    }
}
