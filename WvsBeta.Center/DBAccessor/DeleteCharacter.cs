using WvsBeta.Common;

namespace WvsBeta.Center.DBAccessor
{
    public partial class CharacterDBAccessor
    {
        public static LoginResCode DeleteCharacter(int accountId, int characterId)
        {
            if ((int) _characterDatabaseConnection.RunQuery(
                    "UPDATE characters SET deleted_at = NOW() WHERE ID = @charid AND world_id = @worldid AND userid = @userid",
                    "@charid", characterId,
                    "@worldid", CenterServer.Instance.World.ID,
                    "@userid", accountId) != 1)
            {
                // Unable to delete character
                return LoginResCode.SystemError;
            }
            
            return LoginResCode.SuccessChannelSelect;
        }
    }
}