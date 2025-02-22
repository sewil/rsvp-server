using System.Collections.Generic;
using System.Linq;
using log4net;
using MySqlConnector;

namespace WvsBeta.Game
{
    public class CharacterMonsterBook
    {
        private Character Character { get; }
        public Dictionary<int, byte> Cards { get; } = new Dictionary<int, byte>();
        
        private static ILog log = LogManager.GetLogger("MonsterBookLog");
        
        public struct BookGain
        {
            public int bookId { get; set; }
            public int cardCount { get; set; }
        }
        
        public CharacterMonsterBook(Character character)
        {
            Character = character;
        }

        private void LogGain(int bookId, int cardCount)
        {
            log.Info(new BookGain
            {
                bookId = bookId,
                cardCount = cardCount
            });
        }

        public void Save()
        {
            if (Cards.Count == 0) return;

            Server.Instance.CharacterDatabase.RunTransaction(c =>
            {
                c.CommandText = "INSERT INTO monsterbook (`charid`, `monsterbookid`, `count`) VALUES\n";
                c.CommandText += string.Join(",\n", Cards.Select(x => $"({Character.ID}, {x.Key}, {x.Value})"));
                c.CommandText += "\n ON DUPLICATE KEY UPDATE `count`= VALUES(`count`)";
                c.ExecuteNonQuery();
            });
        }

        public void Load()
        {
            using var data = (MySqlDataReader)Server.Instance.CharacterDatabase.RunQuery(
                "SELECT * FROM monsterbook WHERE charid = @charid",
                "@charid", Character.ID
            );
            while (data.Read())
            {
                Cards[data.GetInt32("monsterbookid")] = (byte)data.GetInt32("count");
            }
        }

        public void SendUpdate()
        {
            CharacterStatsPacket.SendMonsterBook(Character, this);
        }

        public bool TryAddCard(int cardId)
        {
            if (cardId >= 2380000) cardId -= 2380000;

            Cards.TryGetValue(cardId, out var cardCount);
            if (cardCount >= 5)
            {
                MessagePacket.MonsterBookAdded(Character, cardId, cardCount, true);
                return false;
            }

            cardCount++;
            Cards[cardId] = cardCount;


            Character.AddCash(
                cardCount == 5 ? 600 : 100, 
                $"Monster Book {cardId} - x{cardCount}"
            );

            LogGain(cardId, cardCount);
            // Data
            SendUpdate();
            // Animation
            MapPacket.SendGainMonsterBook(Character);
            // Message
            MessagePacket.MonsterBookAdded(Character, cardId, cardCount, false);

            return true;
        }
    }
}