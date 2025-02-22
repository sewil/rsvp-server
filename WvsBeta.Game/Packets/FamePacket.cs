using MySqlConnector;
using WvsBeta.Common.Sessions;

namespace WvsBeta.Game
{
    public static class FamePacket
    {
        public static void HandleFame(Character chr, Packet pr)
        {
            var charId = pr.ReadInt();
            var up = pr.ReadBool();
            var victim = chr.Field.GetPlayer(charId);

            if (charId == chr.ID)
            {
                return;
            }
            
            if (victim == null)
            {
                SendFameError(chr, FameResponse.UserIncorrectlyEntered); // Incorrect User error
                return;
            }
            
            if (chr.PrimaryStats.Level < 15)
            {
                SendFameError(chr, FameResponse.UserUnder15); // Level under 15
                return;
            }

            var query = "";

            if (up)
            {
                // Check if player has been famed already
                query = @"
SELECT 1 
FROM `fame_log` fl
WHERE 
fl.`from` = @from AND
fl.`to` = @to AND 
fl.time >= DATE_SUB(NOW(), INTERVAL 1 MONTH)
";
            }
            else
            {
                // Check if player has been defamed by the same user already

                if (true)
                {
                    query = @"
SELECT 1 
FROM `fame_log` fl
WHERE 
fl.`to` = @to AND 
fl.time >= DATE_SUB(NOW(), INTERVAL 1 MONTH) AND
fl.`from` IN (SELECT id FROM characters WHERE userid = @uid)
";
                }
                else
                {
                    // Check for Unique ID
                    query = @"
SELECT 1 
FROM `fame_log` fl 
-- get characters that have famed this dude
JOIN characters from_char ON from_char.id = fl.from
-- get the users for this dude
JOIN users from_user ON from_user.id = from_char.userid
WHERE 
fl.`to` = @to AND 
fl.time >= DATE_SUB(NOW(), INTERVAL 1 MONTH) AND
from_user.last_unique_id IN (SELECT last_unique_id FROM users WHERE id = @uid)
";
                }
            }



            using (var reader = Server.Instance.CharacterDatabase.RunQuery(query,
                "@to", charId,
                "@from", chr.ID,
                "@uid", chr.UserID
                ) as MySqlDataReader)
            {
                if (reader.Read())
                {
                    // Already famed this person this month
                    SendFameError(chr, FameResponse.CantRaiseThatPersonThisMonth);
                    return;
                }
            }

            using (var reader = Server.Instance.CharacterDatabase.RunQuery("SELECT 1 FROM `fame_log` WHERE `from` = @from AND time >= DATE_SUB(NOW(), INTERVAL 1 DAY)", "@from", chr.ID) as MySqlDataReader)
            {
                if (reader.Read())
                {
                    // Already famed today
                    SendFameError(chr, FameResponse.CantRaiseToday);
                    return;
                }
            }

            victim.AddFame((short) (up ? 1 : -1));
            Server.Instance.CharacterDatabase.RunQuery("INSERT INTO fame_log (`from`, `to`, `time`) VALUES (@from, @to, NOW());", "@from", chr.ID, "@to", victim.ID);
            SendFameSucceed(chr, victim, up);
            
            var t = up ? "gave fame to" : "defamed";

            var msg = $"{chr.Name} (id {chr.ID} userid {chr.UserID}) {t} {victim.Name} (id {victim.ID} userid {victim.UserID}) in map {chr.MapID} ";
            msg += up ? ":arrow_up:" : ":arrow_down:";

            Server.Instance.PlayerLogDiscordReporter.Enqueue(msg);
        }

        public enum FameResponse
        {
            FameOperationSucceeded = 0,
            UserIncorrectlyEntered = 1,
            UserUnder15 = 2,
            CantRaiseToday = 3,
            CantRaiseThatPersonThisMonth = 4,
            YourFameChanged = 5,
            UnkError = 6,
        }

        public static void SendFameError(Character chr, FameResponse error)
        {
            Packet pw = new Packet(ServerMessages.GIVE_POPULARITY_RESULT);
            pw.WriteByte((byte) error);
            chr.SendPacket(pw);
        }

        public static void SendFameSucceed(Character chr, Character victim, bool up)
        {
            Packet pw = new Packet(ServerMessages.GIVE_POPULARITY_RESULT);
            pw.WriteByte((byte) FameResponse.YourFameChanged);
            pw.WriteString(chr.VisibleName);
            pw.WriteBool(up);
            victim.SendPacket(pw);

            pw = new Packet(ServerMessages.GIVE_POPULARITY_RESULT);
            pw.WriteByte((byte) FameResponse.FameOperationSucceeded);
            pw.WriteString(victim.VisibleName);
            pw.WriteBool(up);
            pw.WriteInt(victim.PrimaryStats.Fame);
            chr.SendPacket(pw);
        }
    }
}