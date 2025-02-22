using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Text;

namespace WvsBeta.Center.DBAccessor
{
    public partial class CharacterDBAccessor
    {
        public static bool RenameCharacter(int characterID, string name)
        {
            var recordsAffected = (int)_characterDatabaseConnection.RunQuery(
                "UPDATE characters SET name = @name WHERE id = @characterID AND world_id = @worldid",
                "@name", name,
                "@characterID", characterID,
                "@worldid", CenterServer.Instance.World.ID
            );

            if (recordsAffected != 1)
            {
                _log.Error($"Unable to rename character, only {recordsAffected} records updated. ID: {characterID}, new name: {name}");
                return false;
            }
            
            
            _characterDatabaseConnection.RunQuery(
                "UPDATE buddylist SET buddy_charname = @name WHERE buddy_charid = @characterID",
                "@name", name,
                "@characterID", characterID
            );
            
            _characterDatabaseConnection.RunQuery(
                "UPDATE buddylist_pending SET inviter_charname = @name WHERE inviter_charid = @characterID",
                "@name", name,
                "@characterID", characterID
            );
            
            return true;
        }
    }
}
