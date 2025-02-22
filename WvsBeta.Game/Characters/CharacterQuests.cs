using System;
using System.Collections.Generic;
using System.Linq;
using MySqlConnector;
using WvsBeta.Game.GameObjects;

namespace WvsBeta.Game
{
    public interface IQuestData
    {
        int QuestID { get; }
        string Data { get; set; }
    }

    public class QuestData : IQuestData
    {
        public QuestData(int id, string data)
        {
            QuestID = id;
            Data = data;
        }

        public int QuestID { get; }
        public string Data { get; set; }
    }

    public class KillQuestData : IQuestData
    {
        public KillQuestData(int id, string data, int[] mobs)
        {
            QuestID = id;
            MobIds = mobs;
            KillsLeft = new int[MobIds.Length];
            Data = data;
        }

        public int QuestID { get; }


        private string _actualData;

        public string Data
        {
            get => _actualData ?? KillsLeft.Aggregate("", (current, i) => current + $"{i:D3}");
            set
            {
                for (var i = 0; i < KillsLeft.Length; i++)
                {
                    KillsLeft[i] = 0;
                }

                // Whenever we get data that is not a number, keep using that instead
                if (!value.All(char.IsNumber))
                {
                    _actualData = value;
                    return;
                }

                _actualData = null;


                if (value.Length % 3 != 0) throw new ArgumentException($"Expected killquest to have sets of 3 digits, without spaces, got: {value}");
                var amount = value.Length / 3;

                if (amount != KillsLeft.Length)
                {
                    throw new ArgumentException($"Did not get the same amount of mobs in Data: {value}, {amount}, {KillsLeft.Length}");
                }

                for (var i = 0; i < KillsLeft.Length; i++)
                {
                    KillsLeft[i] = int.Parse(value.Substring(i * 3, 3));
                }
            }
        }

        public int[] KillsLeft { get; }

        // This is an array for convenience, as we can then easily lookup the index of the KillsLeft array
        public int[] MobIds { get; }
    }

    public class CharacterQuests
    {
        private Character Character { get; }
        public Dictionary<int, IQuestData> Quests { get; } = new Dictionary<int, IQuestData>();

        public CharacterQuests(Character character)
        {
            Character = character;
        }

        public void SaveQuests()
        {
            int charid = Character.ID;

            Server.Instance.CharacterDatabase.RunTransaction(x =>
            {
                string query = "";

                query = "DELETE FROM character_quests WHERE charid = " + charid + "; ";

                if (Quests.Count > 0)
                {
                    query += "INSERT INTO character_quests (charid, questid, data) VALUES ";
                    query += string.Join(", ", Quests.Select(kvp =>
                    {
                        return "(" +
                               charid + ", " +
                               kvp.Key + ", " +
                               "'" + MySqlHelper.EscapeString(kvp.Value.Data) + "'" +
                               ")";
                    }));
                    query += ";";
                }

                x.CommandText = query;
                x.ExecuteNonQuery();
            });
        }

        public bool LoadQuests()
        {
            using (var data = (MySqlDataReader) Server.Instance.CharacterDatabase.RunQuery(
                "SELECT * FROM character_quests WHERE charid = @charid",
                "@charid", Character.ID
            ))
            {
                while (data.Read())
                {
                    AddNewQuest(data.GetInt32("questid"), data.GetString("data"), false);
                }
            }

            return true;
        }

        public bool AddNewQuest(int QuestID, string Data = "", bool sendPacket = true)
        {
            if (HasQuest(QuestID))
                return false;

            IQuestData iqd;

            if (QuestsProvider.QuestDemands.TryGetValue(QuestID, out var q) && q.Mobs.Length > 0)
            {
                iqd = new KillQuestData(QuestID, Data, q.Mobs);
            }
            else
            {
                iqd = new QuestData(QuestID, Data);
            }

            Quests[QuestID] = iqd;

            if (sendPacket)
            {
                QuestPacket.SendQuestDataUpdate(Character, QuestID, Data);
            }

            return true;
        }


        public bool HasQuest(int QuestID)
        {
            return Quests.ContainsKey(QuestID);
        }

        public string GetQuestData(int QuestID, string defaultValue = "")
        {
            Quests.TryGetValue(QuestID, out var quest);
            return quest?.Data ?? defaultValue;
        }

        public void AppendQuestData(int QuestID, string pData, bool pSendPacket = true)
        {
            SetQuestData(QuestID, GetQuestData(QuestID) + pData, pSendPacket);
        }


        public void SetQuestData(int QuestID, string pData, bool pSendPacket = true)
        {
            if (!HasQuest(QuestID))
            {
                AddNewQuest(QuestID, pData);
                return;
            }

            Quests[QuestID].Data = pData;
            QuestPacket.SendQuestDataUpdate(Character, QuestID, pData);
        }

        public void SetMobCountQuestInfo(int mobId, List<QuestDemand> questDemands)
        {
            foreach (var qd in questDemands)
            {
                if (!Quests.TryGetValue(qd.ID, out var quest)) continue;

                if (!(quest is KillQuestData kqd))
                {
                    continue;
                }

                var mobIds = kqd.MobIds;
                for (var i = 0; i < mobIds.Length; i++)
                {
                    if (mobIds[i] != mobId) continue;

                    // Now check the count of this one
                    if (kqd.KillsLeft[i] > 0)
                        kqd.KillsLeft[i]--;

                    QuestPacket.SendQuestDataUpdate(Character, quest.QuestID, quest.Data);
                }
            }
        }

        public void RemoveQuest(int questID)
        {
            Quests.Remove(questID);
            QuestPacket.SendQuestRemove(Character, questID);
        }
    }
}