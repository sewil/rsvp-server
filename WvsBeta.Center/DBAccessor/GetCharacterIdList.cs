using System;
using System.Collections.Generic;
using MySqlConnector;

namespace WvsBeta.Center.DBAccessor
{
    public partial class CharacterDBAccessor
    {
        public static IEnumerable<int> GetCharacterIdList(int accountId)
        {
            using (var reader = _characterDatabaseConnection.RunQuery(
                "SELECT id FROM characters WHERE userid = @userId AND world_id = @worldId AND deleted_at IS NULL",
                "@userId", accountId,
                "@worldId", CenterServer.Instance.World.ID
            ) as MySqlDataReader)
            {
                while (reader.Read())
                {
                    yield return reader.GetInt32("id");
                }
            }

        }
    }
}
