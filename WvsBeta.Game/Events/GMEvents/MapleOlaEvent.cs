using System.Collections.Generic;
using System.Linq;
using WvsBeta.Game.GameObjects;
using static WvsBeta.MasterThread;

namespace WvsBeta.Game.Events.GMEvents
{
    class MapleOlaEvent : Event
    {
        private readonly int variation;
        private int StartMapID;
        private Map LobbyMap;
        private List<Map> Maps;

        public MapleOlaEvent(int variation)
        {
            this.variation = variation;
            StartMapID = 109030001 + variation;
            LobbyMap = MapProvider.Maps[StartMapID];
            Maps = new List<int>()
            {
                StartMapID,             //Lobby + s1
                109030002 + variation,  //s2
                109030003 + variation,  //s3
            }.Select(id => MapProvider.Maps[id]).ToList();
            //portal in stage 3 automatically takes all winners to the win map
        }

        private static readonly int LoseMapId = 109050001;
        private static int EventTimeLimitSeconds = 6 * 60; //6 minutes

        private RepeatingAction End = null;

        public override void Prepare()
        {
            EventHelper.CloseAllPortals(Maps);
            base.Prepare();
        }

        public override void Join(Character chr)
        {
            base.Join(chr);
            chr.ChangeMap(StartMapID, LobbyMap.SpawnPoints[0]);
        }

        public override void Start(bool joinDuringEvent = false)
        {
            LobbyMap.ForEachCharacters(c => c.ChangeMap(StartMapID, LobbyMap.SpawnPoints[0]));
            EventHelper.OpenAllPortals(Maps);
            EventHelper.ApplyTimer(Maps, EventTimeLimitSeconds);
            End = RepeatingAction.Start("FitnessWatcher", Stop, EventTimeLimitSeconds * 1000, 0);
            base.Start(joinDuringEvent);
        }

        public override void Stop()
        {
            End?.Stop();
            End = null;
            EventHelper.WarpEveryone(Maps, LoseMapId);
            EventHelper.ResetTimer(Maps);
            base.Stop();
        }
    }
}