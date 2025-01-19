using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WvsBeta.Game.Events;

namespace WvsBeta.Game.GameObjects
{
    class Map_WaitingRoom : Map
    {
        public Map_WaitingRoom(int id) : base(id)
        {

        }

        private void Divide(Character invoker, int fieldID, string portal1, string portal2)
        {
            var left = true;
            ForEachCharacters(chr =>
            {
                if (chr.IsGM) return;
                chr.ChangeMap(fieldID, left ? portal1 : portal2);
                left = !left;
            });

            invoker.ChangeMap(fieldID, portal1);
        }

        public override bool FilterAdminCommand(Character character, CommandHandling.CommandArgs command)
        {
            if (command.Command == "divideteam")
            {
                switch (ID)
                {
                    case Map_Snowball.FIELD_LOBBY:
                        Divide(character, Map_Snowball.FIELD_MAIN, "st01", "st00");
                        break;
                    case 109060003:
                        Divide(character, 109060002, "st01", "st00");
                        break;
                    case 109060005:
                        Divide(character, 109060004, "st01", "st00");
                        break;
                    case Map_AlienHunt.WaitingRoom:
                        Divide(character, Map_AlienHunt.EventRoom, "st01", "st00");
                        break;
                    default:
                        MessagePacket.SendNotice(character, $"Unknown WaitingRoom {ID}");
                        break;
                }

                return true;
            }

            return base.FilterAdminCommand(character, command);
        }
    }
}
