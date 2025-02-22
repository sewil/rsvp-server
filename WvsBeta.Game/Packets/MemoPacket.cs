using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MySqlConnector;
using WvsBeta.Common.Sessions;
using WvsBeta.Database;

namespace WvsBeta.Game
{
    /*
     * Memos, also known as notes.
     */
    class MemoPacket
    {
        public static void OnPacket(Character chr, Packet packet)
        {
            if (packet.ReadByte() == 0) //on delete?
            {
                var memos = packet.ReadByte();
                var memoIds = new int[memos];
                for (int i = 0; i < memos; ++i)
                    memoIds[i] = packet.ReadInt();


                foreach (var t in memoIds.Distinct())
                {
                    Server.Instance.CharacterDatabase.RunQuery(
                        "UPDATE memos SET read_time = @read_time WHERE id = @memo_id AND to_charid = @to_charid",
                        "@read_time", DateTime.Now,
                        "@memo_id", t,
                        "@to_charid", chr.ID
                    );
                }
            }
        }

        public static void SendMemos(Character chr)
        {
            var memos = new List<Memo>();
            using (var query = (MySqlDataReader)Server.Instance.CharacterDatabase.RunQuery(
                "SELECT id, from_name, message, sent_time FROM memos WHERE to_charid = @to_charid AND read_time IS NULL",
                "@to_charid", chr.ID
            ))
            {
                while (query.Read())
                {
                    var fromName = query.GetString("from_name");
                    var id = query.GetInt32("id");
                    var message = query.GetString("message");
                    var time = query.GetDateTime("sent_time");

                    foreach (var line in message.Split('\n').Select(x => x.Trim()))
                    {
                        var lineSplit = line;
                        const int maxLineLength = 100;

                        while (lineSplit.Length != 0)
                        {
                            var part = lineSplit;
                            if (part.Length > maxLineLength) part = part.Remove(maxLineLength);

                            memos.Add(new Memo
                            {
                                from = fromName,
                                id = id,
                                message = part,
                                time = time
                            });
                            
                            lineSplit = lineSplit.Substring(part.Length);
                        }
                    }
                }
            }

            if (memos.Count == 0)
            {
                return;
            }

            var packet = new Packet(ServerMessages.MEMO_RESULT);
            packet.WriteByte(1);
            packet.WriteByte((byte)memos.Count);
            foreach (var memo in memos)
            {
                packet.WriteInt(memo.id);
                packet.WriteString(memo.from);
                packet.WriteString(memo.message);
                packet.WriteLong(memo.time.ToFileTimeUtc());
            }
            chr.SendPacket(packet);
        }

        public static void SendMemoSuccess(Character chr)
        {
            var packet = new Packet(ServerMessages.MEMO_RESULT);
            packet.WriteByte(2);
            chr.SendPacket(packet);
        }

        public static void SendMemoFailure(Character chr, MemoFailureError reason)
        {
            var packet = new Packet(ServerMessages.MEMO_RESULT);
            packet.WriteByte(3);
            packet.WriteByte((byte)reason);
            chr.SendPacket(packet);
        }

        public static bool SendNewMemo(Character chr, string name, string message)
        {
            var otherChar = Server.Instance.GetCharacter(name);
            if (otherChar != null && !chr.IsGM)
            {
                SendMemoFailure(chr, MemoFailureError.OnlinePleaseWhisper);
                return false;
            }

            var charid = Server.Instance.CharacterDatabase.CharacterIdByName(name);
            if (charid == null)
            {
                SendMemoFailure(chr, MemoFailureError.CheckNameOfCharacter);
                return false;
            }

            Server.Instance.CharacterDatabase.SendNoteToUser(
                chr.Name,
                charid.Value,
                message
            );

            SendMemoSuccess(chr);
            if (otherChar != null)
            {
                SendMemos(otherChar);
            }

            return true;
        }

        public enum MemoFailureError
        {
            OnlinePleaseWhisper = 0,
            CheckNameOfCharacter = 1,
            InboxFull = 2,
        }
    }

    public class Memo
    {
        public int id;
        public string from, message;
        public DateTime time;
    }
}



