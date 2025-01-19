using System;
using System.Diagnostics;
using WvsBeta.Common;

namespace WvsBeta.Game
{
    partial class ContinentMan
    {
        public static void Init()
        {
            Instance = new ContinentMan
            {
                ContiMoveArray = new[]
                {
                    new CONTIMOVE
                    {
                        Name = "Ellinia to Orbis",

                        FieldIdCabin = 200090011,
                        FieldIdEnd = 200000100,
                        FieldIdEndShipMove = 200000111,
                        FieldIdMove = 200090010,
                        FieldIdStartShipMove = 101000300,
                        FieldIdWait = 101000301,
                        // Timing
                        
                        WaitMin = 5,
                        EventEndMin = 9,
                        RequiredMin = 10,
                        TermTime = 15,
                        DelayTime = 0,
                        
                        // Mob spawning
                        GetMobItemID = 2100009,
                        MobSpawnPoint = new Pos(485, -221)

                        // Reactors (none)
                    },

                    new CONTIMOVE
                    {
                        Name = "Orbis to Ellinia",

                        FieldIdCabin = 200090001,
                        FieldIdEnd = 101000300,
                        FieldIdEndShipMove = 101000300,
                        FieldIdMove = 200090000,
                        FieldIdStartShipMove = 200000111,
                        FieldIdWait = 200000112,
                        // Timing
                        WaitMin = 5,
                        EventEndMin = 9,
                        RequiredMin = 10,
                        TermTime = 15,
                        DelayTime = 0,

                        // Mob spawning
                        GetMobItemID = 2100009,
                        MobSpawnPoint = new Pos(-590, -221)

                        // Reactors (none)
                    },
                    
                    new CONTIMOVE
                    {
                        Name = "Orbis to Ludibrium",

                        FieldIdEnd = 220000100,
                        FieldIdEndShipMove = 220000110,
                        FieldIdMove = 200090100,
                        FieldIdStartShipMove = 200000121,
                        FieldIdWait = 200000122,
                        // Timing
                        WaitMin = 5,
                        RequiredMin = 5,
                        TermTime = 10,
                        DelayTime = 0,
                    },
                    
                    new CONTIMOVE
                    {
                        Name = "Ludibrium to Orbis",

                        FieldIdEnd = 200000100,
                        FieldIdEndShipMove = 200000121,
                        FieldIdMove = 200090110,
                        FieldIdStartShipMove = 220000110,
                        FieldIdWait = 220000111,
                        // Timing
                        WaitMin = 5,
                        RequiredMin = 5,
                        TermTime = 10,
                        DelayTime = 0,
                    },
                }
            };

            // Normalizing the info
            var currentMinute = MasterThread.CurrentDate.Minute;
            foreach (var contimove in Instance.ContiMoveArray)
            {
                if (contimove.FieldIdStartShipMove == Constants.InvalidMap ||
                    contimove.FieldIdWait == Constants.InvalidMap ||
                    contimove.FieldIdMove == Constants.InvalidMap ||
                    contimove.FieldIdEnd == Constants.InvalidMap ||
                    contimove.FieldIdEndShipMove == Constants.InvalidMap)
                {
                    throw new Exception("Continent Info : Doesn't exist FieldID");
                }

                if (contimove.TermTime <= 0)
                    throw new Exception($"Continent Info : Invalid schedule term : {contimove.TermTime}");


                int startMinute = contimove.DelayTime - contimove.TermTime - contimove.WaitMin;
                if (startMinute >= 60) throw new Exception();

                int termOffset = 0;
                int termNegativeOffset = -contimove.TermTime; // For 'just begin of hour' (-10 minutes)???
                int iterations = 0;
                while (
                    startMinute > currentMinute ||
                    (contimove.DelayTime + termOffset - contimove.WaitMin) <= currentMinute
                )
                {
                    termOffset += contimove.TermTime;
                    termNegativeOffset += contimove.TermTime;
                    iterations++;
                    startMinute = contimove.DelayTime + termNegativeOffset - contimove.WaitMin;

                    if (startMinute >= 60) throw new Exception();
                }


                int nextBoardingMinutes = contimove.TermTime * iterations - contimove.WaitMin;
                nextBoardingMinutes += contimove.DelayTime;
                bool wrapped = false;
                if (nextBoardingMinutes >= 60)
                {
                    wrapped = true;
                    nextBoardingMinutes -= contimove.TermTime;
                }
                
                // Calculate the 'minute' on the hour...

                var dt = MasterThread.CurrentDate;
                dt = new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, nextBoardingMinutes, 0);
                if (wrapped)
                {
                    dt = dt.AddMinutes(60);
                    dt = dt.AddMinutes(contimove.TermTime);
                }
                var millis = (long)(dt - MasterThread.CurrentDate).TotalMilliseconds;

                contimove.NextBoardingTime = MasterThread.CurrentTime + millis;

                contimove.State = Conti.Dormant;

                contimove.ResetEvent();

                Trace.WriteLine($"{contimove.Name} next boards at {TimeSpan.FromMilliseconds(millis)}");
            }

            MasterThread.RepeatingAction.Start(
                "ContinentMan Update", 
                Instance.Update,
                TimeSpan.Zero, 
                TimeSpan.FromSeconds(1)
            );
        }
    }
}