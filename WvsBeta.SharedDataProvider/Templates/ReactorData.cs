using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using WvsBeta.SharedDataProvider.Providers;
using WzTools.Objects;

namespace WvsBeta.SharedDataProvider.Templates
{
    public class ReactorData
    {
        public string L0 { get; set; }
        public string L1 { get; set; }
        public string L2 { get; set; }
        public string ID => L2;

        public int ReqHitCount { get; set; }

        public bool RemoveInFieldSet { get; set; }

        public int StateCount { get; set; }

        public StateData[] States { get; set; } = new StateData[0];

        public ActionData[] Actions { get; set; } = new ActionData[0];

        public DropData[] RewardInfo { get; set; } = new DropData[0];

        public static IEnumerable<T> LoadArgs<T>(WzProperty prop) => TemplateProvider.LoadArgs<T>(prop);

        public static IEnumerable<object> LoadArgs(WzProperty prop) => LoadArgs<object>(prop);

        /// <summary>
        /// Get sum of all animation frames of the Reactor of a particular state
        /// </summary>
        /// <param name="stateProp">The property of the state, containing 0..n, event, and hit subprops</param>
        /// <returns>Sum of all the animation nodes' delay props</returns>
        public static int GetSumDelay(WzProperty stateProp)
        {
            int sumDelay = 0;

            TemplateProvider.IterateOverIndexed(stateProp, (_, x) => sumDelay += x.GetInt32("delay") ?? 120);

            return sumDelay;
        }

        public override string ToString()
        {
            return $"{L0}/{L1}/{L2}";
        }

        public class StateData
        {
            public EventData[] Events { get; set; } = new EventData[0];

            // Time to hitable
            public int HitDelay { get; set; }
            // Time to next state, based on sum(hit.n.delay)
            public int ChangeStateDelay { get; set; }
            public int Timeout { get; set; }
            public bool End { get; set; }
        }

        public class ActionData
        {
            /// <summary>
            /// The comments in this Enum map to the arguments.
            /// </summary>
            public enum Types
            {
                // [0: bool, onlyAttacker] [string
                TransferPlayer = 0,

                // Summon one or more mobs
                // [0: mobid] [1: summon type] [2: summon amount] ([3: summon mobType] ([4: x] [5: y]))
                SummonMob = 1,

                // Spawn rewards from ReactorReward
                Reward = 2,

                // NOT IMPLEMENTED
                // [0: on/off]
                // Has separate 'pn' node for Portal Name
                TogglePortal = 3,

                // [0: npcid] [1: x] [2: y]
                SummonNPC = 6,

                // [0: bHeavyNShortTremble] [1: tDelay]
                TrembleField = 7,

                // 
                RunScript = 10,

                // For MonsterCarnival
                RunOnGuardianDestroyed = 11,
            }

            public Types Type { get; set; }
            public int State { get; set; }
            public string Message { get; set; }
            public object[] Args { get; set; }

            public override string ToString()
            {
                return $"ReactorActionData type {Type} state {State} message {Message} Args: {string.Join(", ", Args)}";
            }
        }

        public class SummonActionData : ActionData
        {
            public int MobID => (int) Args[0];
            public int SummonType => (int) Args[1];
            public int SummonAmount => (int) Args[2];
            public bool HasSummonMobType => Args.Length >= 4;
            public int SummonMobType => HasSummonMobType ? (int) Args[3] : 0;
            public bool HasXY => Args.Length >= 5;
            public int X => HasXY ? (int) Args[4] : 0;
            public int Y => HasXY ? (int) Args[5] : 0;
        }

        public class SummonNpcActionData : ActionData
        {
            public int NpcID => (int) Args[0];
            public bool HasXY => Args.Length >= 2;
            public int X => HasXY ? (int) Args[1] : 0;
            public int Y => HasXY ? (int) Args[2] : 0;
        }

        public class RewardActionData : ActionData
        {
        }


        public class TransferActionData : ActionData
        {
            public bool MultiMap => Args.Length > 1;
            public bool AllPlayers => Args.Length > 1 && (int) Args[0] == 0;
            public int MapCount => MultiMap ? (Args.Length - 1) / 2 : 1;

            public int GetMapID(int idx) => (int)Args[1 + (idx * 2) + 0];
            public string GetPortalName(int idx) => (string)Args[1 + (idx * 2) + 1];
        }

        public class TogglePortalActionData : ActionData
        {
            public string PortalName { get; set; }
            public bool Enabled => (int)Args[0] == 1;
        }

        public class FindItemUpdateEventData : EventData
        {
            public (int ItemID, int Amount)[] Items { get; private set; }
            public bool RemoveDrops { get; private set; }

            public override void Load()
            {
                Items = new (int ItemID, int Amount)[Args.Length / 2];

                for (var i = 0; i < Items.Length; i++)
                {
                    Items[i].ItemID = Args[(i * 2) + 0];
                    Items[i].Amount = Args[(i * 2) + 1];
                }

                RemoveDrops = Args[Args.Length - 1] != 0;
            }
        }



        public class EventData
        {
            public enum Types
            {
                Hit = 0,
                HitLeft = 1,
                HitRight = 2,
                HitJumpLeft = 3,
                HitJumpRight = 4,

                // Special events
                FindItemUpdate = 100,
                TimeoutReset = 101,
            }

            public byte ID { get; set; }

            public Types Type { get; set; }

            public byte StateToBe { get; set; }

            public int HitDelay { get; set; }

            public Rectangle? CheckArea { get; set; }

            public int[] Args { get; set; }

            /// <summary>
            /// Get the priority level for the hittype (sent by client) compared to the Type of this event.
            /// </summary>
            /// <param name="option">The hit Option field from the client.</param>
            /// <returns>A value you can use for Sort()</returns>
            public int GetHittypePriorityLevel(uint option)
            {
                var flipped = (option & 1) == 1;

                if ((option & 2) != 0)
                {
                    return Type switch
                    {
                        Types.Hit => 1,
                        Types.HitLeft => flipped ? -1 : 0,
                        Types.HitRight => flipped ? 0 : -1,
                        _ => -1
                    };
                }
                else
                {
                    return Type switch
                    {
                        Types.Hit => 2,
                        Types.HitLeft => flipped ? -1 : 1,
                        Types.HitRight => flipped ? 1 : -1,
                        Types.HitJumpLeft => flipped ? -1 : 0,
                        Types.HitJumpRight => flipped ? 0 : -1,
                        _ => -1
                    };
                }
            }

            public virtual void Load() {}
        }
    }
}