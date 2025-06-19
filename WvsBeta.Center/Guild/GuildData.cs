using System.Collections.Generic;
using System.Linq;
using MySqlConnector;
using WvsBeta.Common;
using static WvsBeta.Common.GuildCharacter;

namespace WvsBeta.Center.Guild
{
    public static class GuildData
    {
        public static List<Common.Guild> LoadGuilds(params int[] guildIds)
        {
            var guilds = new List<Common.Guild>();

            var query = "SELECT * FROM guilds";
            if (guildIds.Length > 0)
            {
                query += " WHERE id IN (" + string.Join(", ", guildIds) + ")";
            }

            using (var guildData =
                CenterServer.Instance.CharacterDatabase.RunQuery(query) as MySqlDataReader)
            {
                while (guildData != null && guildData.Read())
                {
                    var guildID = guildData.GetInt32("id");

                    guilds.Add(new Common.Guild
                    {
                        ID = guildID,
                        Name = guildData.GetString("name"),
                        Capacity = (byte)guildData.GetInt32("capacity"),
                        Logo = new GuildLogo
                        {
                            Background = guildData.GetInt16("logo_bg"),
                            BackgroundColor = (byte)guildData.GetInt16("logo_bg_color"),
                            Foreground = guildData.GetInt16("logo_fg"),
                            ForegroundColor = (byte)guildData.GetInt16("logo_fg_color"),
                        },
                    });
                }
            }

            foreach (var guild in guilds)
            {
                guild.Characters.Clear();
                guild.Characters.AddRange(LoadCharactersForGuild(guild.ID));
            }

            return guilds;
        }

        public static IEnumerable<Common.GuildCharacter> LoadCharactersForGuild(int guildID)
        {
            using var guildCharacters = CenterServer.Instance.CharacterDatabase.RunQuery("SELECT ID, `name`, `level`, `job`, `guild_rank` FROM characters WHERE guild_id = @guildid", "guildid", guildID) as MySqlDataReader;

            while (guildCharacters != null && guildCharacters.Read())
            {
                yield return new Common.GuildCharacter
                {
                    CharacterID = guildCharacters.GetInt32("ID"),
                    Job = guildCharacters.GetInt16("job"),
                    Level = guildCharacters.GetByte("level"),
                    CharacterName = guildCharacters.GetString("name"),
                    Rank = (Common.GuildCharacter.Ranks)guildCharacters.GetInt32("guild_rank"),
                };
            }
        }

        public static bool LeaveGuild(int characterID)
        {
            return (int)CenterServer.Instance.CharacterDatabase.RunQuery(
                "UPDATE characters SET guild_id = NULL, guild_rank = 0 WHERE ID = @char AND guild_id IS NOT NULL",
                "@char", characterID
            ) == 1;
        }

        public static bool JoinGuild(int characterID, int guildID, GuildCharacter.Ranks rank)
        {
            CenterServer.Instance.CharacterDatabase.RunQuery(
                "UPDATE characters SET guild_id = @guildid, guild_rank = @guildRank WHERE ID = @char",
                "@guildid", guildID,
                "@char", characterID,
                "@guildRank", (int)rank
            );

            return true;
        }

        public static bool ResizeGuild(int guildID, byte newSize)
        {
            CenterServer.Instance.CharacterDatabase.RunQuery(
                "UPDATE guilds SET capacity = @capacity WHERE id = @guildid",
                "@guildid", guildID,
                "@capacity", newSize
            );

            return true;
        }

        public static int CreateGuild(int guildMaster, string guildName)
        {
            CenterServer.Instance.CharacterDatabase.RunQuery(
                "INSERT INTO guilds SET `guildmaster_id` = @master, `name` = @name",
                "@master", guildMaster,
                "@name", guildName
            );

            return CenterServer.Instance.CharacterDatabase.GetLastInsertId();
        }

        public static bool DisbandGuild(int guildId)
        {
            CenterServer.Instance.CharacterDatabase.RunQuery(
                "DELETE FROM `guilds` WHERE id = @guildid",
                "@guildid", guildId
            );

            CenterServer.Instance.CharacterDatabase.RunQuery(
                "UPDATE characters SET guild_id = NULL, guild_rank = 0 WHERE guild_id = @guildid",
                "@guildid", guildId
            );

            return true;
        }

        public static bool RenameGuild(int guildId, string name)
        {
            return (int)CenterServer.Instance.CharacterDatabase.RunQuery(
                "UPDATE `guilds` SET name = @name WHERE id = @guildid",
                "@guildid", guildId,
                "@name", name
            ) == 1;
        }

        public static bool ChangeGuildLogo(int guildId, GuildLogo logo)
        {
            return (int)CenterServer.Instance.CharacterDatabase.RunQuery(
                "UPDATE `guilds` SET logo_bg = @logo_bg, logo_bg_color = @logo_bg_color, logo_fg = @logo_fg, logo_fg_color = @logo_fg_color WHERE id = @guildid",
                "@guildid", guildId,
                "@logo_bg", logo.Background,
                "@logo_bg_color", logo.BackgroundColor,
                "@logo_fg", logo.Foreground,
                "@logo_fg_color", logo.ForegroundColor
            ) == 1;
        }

        public static bool ChangeRank(int characterId, GuildCharacter.Ranks rank)
        {
            return (int)CenterServer.Instance.CharacterDatabase.RunQuery(
                "UPDATE characters SET guild_rank = @guildRank WHERE id = @characterId",
                "@characterId", characterId,
                "@guildRank", (int)rank
            ) == 1;
        }

        public static bool DemoteGuildMaster(int guildId, int oldMasterId, int newMasterId)
        {
            int records = 0;
            CenterServer.Instance.CharacterDatabase.RunTransaction(comm =>
            {
                comm.CommandText = "UPDATE characters SET guild_rank = 2 WHERE id = @oldMasterId";
                comm.Parameters.AddWithValue("@oldMasterId", oldMasterId);
                records = comm.ExecuteNonQuery();
                comm.Parameters.Clear();

                comm.CommandText = "UPDATE characters SET guild_rank = 3 WHERE id = @newMasterId";
                comm.Parameters.AddWithValue("@newMasterId", newMasterId);
                records += comm.ExecuteNonQuery();
                comm.Parameters.Clear();

                comm.CommandText = "UPDATE guilds SET guildmaster_id = characterId WHERE id = @guildId";
                comm.Parameters.AddWithValue("@guildId", guildId);
                records += comm.ExecuteNonQuery();
                comm.Parameters.Clear();
            }, Program.MainForm.LogAppend);
            return records > 0;
        }
    }
}