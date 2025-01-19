using System;
using WvsBeta.Common;
using WvsBeta.Game;

namespace WvsBeta.SharedDataProvider.Templates
{
    public class MobGenItem : LimitedObject
    {
        public int ID { get; }
        public int RegenInterval { get; set; }
        public long RegenAfter { get; set; }
        public short Foothold { get; }
        public bool FacesLeft { get; }
        public int MobCount { get; set; }

        private bool _initializedYAxis;
        private short _y, _cy;
        public short X { get; }

        public short Y
        {
            get
            {
                if (_initializedYAxis) return _y;

                MobData md;
                if (DataProvider.Mobs.TryGetValue(ID, out md) == false)
                {
                    Console.WriteLine($"Invalid mob template ID({ID})");
                    return -1;
                }

                // Flying mobs use CY value
                //if (md.Flies)
                    _y = _cy;
                _initializedYAxis = true;
                return _y;
            }
        }
        
        public MobGenItem(Life life, long? currentTime)
        {
            ID = life.ID;
            Foothold = (short)life.Foothold;
            FacesLeft = life.FacesLeft;
            X = life.X;
            _initializedYAxis = false;
            _y = life.Y;
            _cy = life.Cy;

            MobCount = 0;
            RegenAfter = 0;
            RegenInterval = life.RespawnTime * 1000;

            if (RegenInterval >= 0)
            {
                var baseTime = RegenInterval / 10;
                var maxAdditionalTime = 6 * RegenInterval / 10;
                
                RegenAfter = baseTime;
                if (maxAdditionalTime > 0)
                    RegenAfter += Rand32.Next() % maxAdditionalTime;

                RegenAfter += currentTime ?? MasterThread.CurrentTime;
            }
        }


    }
}