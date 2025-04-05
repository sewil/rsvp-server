using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WvsBeta.SharedDataProvider.Templates
{
    public static class FieldSetData
    {
        public class ActionData
        {
            public object[] Args { get; set; }
        }

        public class EventData
        {
            public enum Actions
            {
                // Shows the GM event notice info
                ShowDesc = 0,
                // Changes the time left/clock of the map. Arg 0 is time in seconds
                UpdateClock = 1,
                // Enables a portal. [mapidx] [portalname] [enabled when !0]
                TogglePortal = 2,
                // Calls FieldSet::BroadcastMsg([arg1], [arg2], [arg3 if arg1 == 7, otherwise 0])
                BroadcastMsg = 3,
                // Calls Field::Reset()
                ResetAllMaps = 4,
                // Calls CField_SnowBall::Conclude
                SnowballConclude = 5,
                // Calls FieldSet::CastOut, for all players, no target map (0) or portal ("")
                CastOut = 6,
                // Sends a red warning text to everyone in the channel. [msg]
                SendChannelRedText = 7,
                // Spawns a mob. [mapidx] [mobid] [x] [y]
                SpawnMob = 8,
                // Runs weather effect. [mapidx] [itemid] [message]
                WeatherEffect = 9,
                // Runs Special Action of an NPC. [mapidx] [npc name (!)] [action str]
                SetNPCSpecialAction = 10,
            }

            public int TimeAfter { get; set; }
            public Actions Action { get; set; }
            public object[] Args { get; set; }

            public int Index { get; set; }
        }

        public class ShowDescEventData : EventData
        {
        }

        public class UpdateClockEventData : EventData
        {
            public int TimeLeft => (int)Args[0];
        }

        public class TogglePortalEventData : EventData
        {
            public int MapIndex => (int)Args[0];
            public string PortalName => (string)Args[1];
            public bool Enable => (int)Args[2] != 0;
        }

        public class BroadcastMsgEventData : EventData
        {
            public int Type => (int)Args[0];
            public string Message => (string)Args[1];
            public int TemplateID => Type == 7 ? (int)Args[2] : 0;
        }

        public class ResetAllMapsEventData : EventData
        {
        }

        public class SnowballConcludeEventData : EventData
        {
        }
        public class SendChannelRedTextEventData : EventData
        {
            public string Message => (string)Args[0];
        }
        public class SpawnMobEventData : EventData
        {
            public int MapIndex => (int)Args[0];
            public int TemplateID => (int)Args[1];
            public int X => (int)Args[2];
            public int Y => (int)Args[3];
        }

        public class ReactorActionInfo
        {

            public enum Types
            {
                // [mapidx] [reactorname] [state]
                ChangeReactorState = 3,

                // [variablename] [value]
                SetFieldsetVariable = 4,

                // [mapidx] [song]
                ChangeMusic = 5,

                // [heavynshort] [delay] [mapidx...]
                // Not in this version (v.12)
                Tremble = 6,

                // [mapidx]
                // Not in this version (v.12)
                ResetMobs = 7,

                // [mapidx]
                // Not in this version (v.12)
                SpawnNPC = 8,

                // [mapidx] [objectname] [state]
                // Not in this version (v.12)
                SetFieldObjectState = 9,
            }

            public object[] Args { get; set; }
            public int Index { get; set; }

            // The map index which this action needs to be handled in
            public int DefinedMapIndex { get; set; }
            
            // 'ari'... pretty bad var name
            public List<(string ReactorName, int EventState)> ReactorInfo { get; } = new List<(string ReactorName, int EventState)>();
        }

        public class ChangeReactorStateReactorActionInfo : ReactorActionInfo
        {
            public int MapIndex => (int)Args[0];
            public string ReactorName => (string)Args[1];
            public byte? State => Args.Length >= 3 ? (byte?)(int)Args[2] : null;
        }

        public class SetFieldsetVariableReactorActionInfo : ReactorActionInfo
        {
            public string VariableName => (string)Args[0];
            public string Value => (string)Args[1];
        }

        public class SpawnNPCReactorActionInfo : ReactorActionInfo
        {
            public int MapIndex => (int)Args[0];
        }

        public class SetFieldObjectStateReactorActionInfo : ReactorActionInfo
        {
            public int MapIndex => (int)Args[0];
            public string ObjectName => (string)Args[1];
            public byte? State => Args.Length >= 3 ? (byte?)(int)Args[2] : null;
        }

        public class ChangeMusicReactorActionInfo : ReactorActionInfo
        {
            public int MapIndex => (int)Args[0];
            public string Song => (string)Args[1];
        }

        public class TrembleReactorActionInfo : ReactorActionInfo
        {
            public bool HeavyNShort => (int)Args[0] == 1;
            public int Delay => (int)Args[1];
            public int[] MapIndice => Args.Skip(2).Cast<int>().ToArray();
        }
    }
}
