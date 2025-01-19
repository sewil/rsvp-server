namespace WvsBeta.Common
{
    public enum FriendResReq
    {
        FriendReq_LoadFriend,
        FriendReq_SetFriend,
        FriendReq_AcceptFriend,
        FriendReq_RefuseDeleteFriend,

        FriendRes_LoadFriend_Done = 7,
        FriendRes_NotifyChange_FriendInfo,
        FriendRes_Invite,
        FriendRes_SetFriend_Done,
        FriendRes_SetFriend_FullMe,
        FriendRes_SetFriend_FullOther,
        FriendRes_SetFriend_AlreadySet,
        FriendRes_SetFriend_Master,
        FriendRes_SetFriend_UnknownUser,
        FriendRes_SetFriend_Unknown,
        FriendRes_SetFriend_RemainCharacterFriend,
        FriendRes_DeleteFriend_Done,
        FriendRes_DeleteFriend_Unknown,
        FriendRes_Notify,
        FriendRes_IncMaxCount_Done,
        FriendRes_IncMaxCount_Unknown,
    }
}