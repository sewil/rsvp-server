using System;
using System.Collections.Generic;
using System.Text;

namespace WvsBeta.Common
{
    public enum LoginResCode : byte
    {
        SuccessChannelSelect = 0,
        SuccessLogin = 1,
        Banned = 2,
        DeletedOrBlocked = 3,
        InvalidPassword = 4,
        AccountDoesNotExist = 5,
        // Connection failed due to system error
        SystemError = 6,
        AlreadyOnline = 7,
        NotConnectableWorld = 8,
        // Just following Nexon naming
        Unknown = 9,
        // Too many requests
        Timeout = 10,
        NotAdult = 11,
        // Not used?
        AuthFail = 12,
        MasterCannotLoginOnThisIP = 13,


        VerifyEmail = 16,
        WrongGatewayOrChangeInfo = 17,
        InvalidDoB = 18,
        ConfirmEULA = 19,
    }
}
