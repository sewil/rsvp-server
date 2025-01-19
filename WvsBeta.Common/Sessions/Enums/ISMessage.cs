namespace WvsBeta.Common.Sessions
{
    /// <summary>
    /// These are messages sent from clients to Center
    /// </summary>
    public enum ISClientMessages : byte
    {
        Ping = (byte)ServerMessages.PING,
        Pong = (byte)ClientMessages.PONG,
        OFFSET = 30, // Make sure we do not conflict with ping/pong

        ServerRequestAllocation,
        ServerSetConnectionsValue,
        ServerRegisterUnregisterPlayer,

        PlayerChangeServer,
        PlayerQuitCashShop,
        PlayerRequestWorldLoad,
        PlayerRequestChannelStatus,
        PlayerWhisperOrFindOperation,
        PlayerUsingSuperMegaphone,

        MessengerJoin,
        MessengerLeave,
        MessengerInvite,
        MessengerBlocked,
        MessengerDeclined,
        MessengerChat,
        MessengerAvatar,

        PartyCreate,
        PartyInvite,
        PartyAccept,
        PartyLeave,
        PartyExpel,
        PartyDisconnect,
        PartyDecline,
        PartyChat,
        PartyDoorChanged,

        RequestBuddylist,
        BuddyUpdate,
        BuddyInvite,
        BuddyInviteAnswer,
        BuddyListExpand,
        BuddyDisconnect,
        BuddyChat,
        BuddyDeclineOrDelete,
        
        GuildKickPlayer,
        GuildJoinPlayer,
        GuildLeavePlayer,
        GuildUpdatePlayer,
        GuildResize,
        GuildDisband,
        GuildCreate,
        GuildChat,
        GuildReload,
        GuildRankUpdate,
        GuildRename,
        GuildChangeLogo,

        AdminMessage,
        
        ChangeRates,
        PlayerUpdateMap, //Used for parties :/
        ServerMigrationUpdate,
        PlayerCreateCharacterNamecheck,
        PlayerCreateCharacter,
        PlayerDeleteCharacter,

        KickPlayer,
        UpdatePlayerJobLevel,

        BroadcastPacketToGameservers,
        BroadcastPacketToShopservers,
        BroadcastPacketToAllServers,
        ReloadEvents,

        RenamePlayer,

        UpdatePublicIP,
    }

    /// <summary>
    /// These are messages sent from Center to its clients
    /// </summary>
    public enum ISServerMessages : byte
    {
        Ping = (byte)ServerMessages.PING,
        Pong = (byte)ClientMessages.PONG,
        OFFSET = 30, // Make sure we do not conflict with ping/pong

        ServerAssignmentResult,
        ServerSetUserNo, // For Centers -> Logins

        PlayerChangeServerData,
        PlayerChangeServerResult,
        PlayerRequestWorldLoadResult,
        PlayerRequestChannelStatusResult,
        PlayerWhisperOrFindOperationResult,
        PlayerSuperMegaphone,

        PlayerSendPacket,

        ChangeRates,

        AdminMessage,

        ChangeParty,
        UpdateHpParty,
        PartyInformationUpdate,
        PartyDisbanded,

        ServerMigrationUpdate,
        ChangeCenterServer,
        PlayerCreateCharacterNamecheckResult,
        PlayerCreateCharacterResult,
        PlayerDeleteCharacterResult,
        
        GuildUpdate,
        GuildUpdateSingle,
        GuildLeavePlayer,
        GuildJoinPlayer,
        GuildUpdatePlayer,
        GuildDisbanded,
        GuildResized,
        GuildChat,
        GuildRename,
        GuildChangeLogo,

        KickPlayerResult,
        
        WSE_ChangeScrollingHeader,
        ReloadNPCScript,
        ReloadCashshopData,
        PlayerRenamed,

        PublicIPUpdated,
        UpdateHaProxyIPs,
    }
}
