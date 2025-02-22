using MySqlConnector;

namespace WvsBeta.Game
{
    public class CharacterGameStats
    {
        public int CharacterID { get; private set; }

        public int OmokWins { get; set; }
        public int OmokTies { get; set; }
        public int OmokLosses { get; set; }
        public int OmokScore { get; set; }

        public int MatchCardWins { get; set; }
        public int MatchCardTies { get; set; }
        public int MatchCardLosses { get; set; }
        public int MatchCardScore { get; set; }

        public CharacterGameStats(Character pCharacter)
        {
            CharacterID = pCharacter.ID;
        }

        private const int DefaultPoints = 2000;

        public void Load()
        {
            OmokScore = DefaultPoints;
            MatchCardScore = DefaultPoints;

            using var data = (MySqlDataReader) Server.Instance.CharacterDatabase.RunQuery(
                "SELECT * FROM gamestats WHERE id = @id",
                "@id", CharacterID
            );
            if (!data.Read()) return;

            OmokWins = data.GetInt32("omokwins");
            OmokTies = data.GetInt32("omokties");
            OmokLosses = data.GetInt32("omoklosses");
            OmokScore = data.GetInt32("omokscore");

            MatchCardWins = data.GetInt32("matchcardwins");
            MatchCardTies = data.GetInt32("matchcardties");
            MatchCardLosses = data.GetInt32("matchcardlosses");
            MatchCardScore = data.GetInt32("matchcardscore");
        }

        public void Save()
        {
            Server.Instance.CharacterDatabase.RunQuery(
                "INSERT INTO gamestats (id, omokwins, omokties, omoklosses, omokscore, matchcardwins, matchcardties, matchcardlosses, matchcardscore) VALUES (" + 
                "@charid, @ow, @ot, @ol, @os, @mw, @mt, @ml, @ms) "+
                "ON DUPLICATE KEY UPDATE" + 
                "  omokwins = @ow" + 
                ", omokties = @ot" + 
                ", omoklosses = @ol" +
                ", omokscore = @os",
                ", matchcardwins = @mw" + 
                ", matchcardties = @mt" + 
                ", matchcardlosses = @ml",
                ", matchcardscore = @ms",
                "@charid", CharacterID,
                "@ow", OmokWins,
                "@ot", OmokTies,
                "@ol", OmokLosses,
                "@os", OmokScore,
                "@mw", MatchCardWins,
                "@mt", MatchCardTies,
                "@ml", MatchCardLosses,
                "@ms", MatchCardScore
            );
        }
    }
}
