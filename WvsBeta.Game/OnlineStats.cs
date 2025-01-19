using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using log4net;
using Newtonsoft.Json;

namespace WvsBeta.Game
{
    public class OnlineStats
    {
        private static ILog _log = LogManager.GetLogger("OnlineStatsLog");

        public class Category
        {
            public int Total;
            
            public int Level0x;
            public int Level1x;
            public int Level2x;
            public int Level3x;
            public int Level4x;
            public int Level5x;
            public int Level6x;
            public int Level7x;
            public int Level8x;
            public int Level9x;
            public int Level10x;
            public int Level11x;
            public int Level12x;
            public int Level13x;
            public int Level14x;
            public int Level15x;
            public int Level16x;
            public int Level17x;
            public int Level18x;
            public int Level19x;
            public int Level20x;

            [JsonIgnore]
            public List<string> Names { get; } = new List<string>();

            public void Calculate(IEnumerable<Character> chrs)
            {
                Total = 0;
                Names.Clear();
                Level0x = 0;
                Level1x = 0;
                Level2x = 0;
                Level3x = 0;
                Level4x = 0;
                Level5x = 0;
                Level6x = 0;
                Level7x = 0;
                Level8x = 0;
                Level9x = 0;
                Level10x = 0;
                Level11x = 0;
                Level12x = 0;
                Level13x = 0;
                Level14x = 0;
                Level15x = 0;
                Level16x = 0;
                Level17x = 0;
                Level18x = 0;
                Level19x = 0;
                Level20x = 0;


                foreach (var character in chrs)
                {
                    Total++;
                    var level = character.Level;
                    if (level > 200) level = 200;
                    level /= 10;
                    switch (level)
                    {
                        case 0: Level0x++; break;
                        case 1: Level1x++; break;
                        case 2: Level2x++; break;
                        case 3: Level3x++; break;
                        case 4: Level4x++; break;
                        case 5: Level5x++; break;
                        case 6: Level6x++; break;
                        case 7: Level7x++; break;
                        case 8: Level8x++; break;
                        case 9: Level9x++; break;
                        case 10: Level10x++; break;
                        case 11: Level11x++; break;
                        case 12: Level12x++; break;
                        case 13: Level13x++; break;
                        case 14: Level14x++; break;
                        case 15: Level15x++; break;
                        case 16: Level16x++; break;
                        case 17: Level17x++; break;
                        case 18: Level18x++; break;
                        case 19: Level19x++; break;
                        case 20: Level20x++; break;
                    }

                    Names.Add(character.Name);
                }
            }
        }

        public static OnlineStats Instance { get; } = new OnlineStats();

        public Category Total { get; } = new Category();
        public Category GMs { get; } = new Category();
        public Category Husks { get; } = new Category();
        public Category AFKs { get; } = new Category();
        public Category ActivePlayers { get; } = new Category();

        public void Calculate()
        {
            var allCharacters = Server.Instance.CharacterList.Values.ToArray();

            Total.Calculate(allCharacters);
            GMs.Calculate(allCharacters.Where(x => x.IsGM));

            var nonGMs = allCharacters.Where(x => !x.IsGM).ToArray();

            Husks.Calculate(nonGMs.Where(x => x.HuskMode));
            AFKs.Calculate(nonGMs.Where(x => x.IsAFK));
            ActivePlayers.Calculate(nonGMs.Where(x => !x.IsAFK && !x.HuskMode));
        }

        public static void StartLogger()
        {
            MasterThread.RepeatingAction.Start(
                "OnlineStats", () =>
                {
                    Instance.Calculate();
                    _log.Info(Instance);
                },
                TimeSpan.Zero,
                TimeSpan.FromMinutes(1)
            );
        }
    }
}
