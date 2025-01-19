using System;
using System.Collections.Generic;
using System.Text;

namespace WvsBeta.Common.Sessions
{
    public enum CfgServerMessages
    {
        CFG_RSA_CHALLENGE,
        CFG_TELEPORT,
        CFG_INVOKE_PATCHER,
        CFG_TOTP,
        CFG_MONSTER_BOOK,
        CFG_MONSTER_BOOK_ADDED,
        CFG_GUILD,
        CFG_SCRMSG,
        CFG_RETURN_TO_LOGIN,
        CFG_RATECREDITS,
        CFG_COMMUNICATOR,
        CFG_TOGGLE_UI,
    }

    public enum CfgClientMessages
    {
        CFG_FILE_CHECKSUM, 
        CFG_MEMORY_EDIT_DETECTED,
        __CUSTOM_DC_ME__,
        CFG_LOGIN_PUBKEY,
        CFG_GUILD,
        CFG_BACKUP_PACKET,
        CFG_TOTP,
        CFG_COMMUNICATOR,
    }
}
