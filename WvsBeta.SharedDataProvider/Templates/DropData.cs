using System;
using WvsBeta.Game;

namespace WvsBeta.SharedDataProvider.Templates
{
    public class DropData : LimitedObject
    {
        public int ItemID { get; set; }
        public int Mesos { get; set; }
        public short Min { get; set; }
        public short Max { get; set; }
        public bool Premium { get; set; }
        public int Chance { get; set; }
        // Expires after X minutes
        public ushort Period { get; set; }
        // Expires on exact date
        public DateTime DateExpire { get; set; }
        
        
        public int MobMinLevel { get; set; }
        public int MobMaxLevel { get; set; }

        public ItemVariation ItemVariation { get; set; }

        public const double DropChanceCalcFloat = 1000000000.0;
        public const int DropChanceCalcInt = 1000000000;
    }
}