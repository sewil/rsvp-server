using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using WvsBeta.Common;
using WvsBeta.Common.Sessions;
using WvsBeta.Game.GameObjects;
using WvsBeta.Game.GameObjects.MiniRooms;
using WvsBeta.Game.Packets;
using WvsBeta.SharedDataProvider.Templates;

namespace WvsBeta.Game
{
    public static class MapPacket
    {
        [Flags]
        public enum AvatarModFlag
        {
            Skin = 1,
            Face = 2,
            Equips = 4,
            ItemEffects = 8 | 0x10,
            Speed = 0x10,
            Rings = 0x20
        }

        public static void HandleMove(Character chr, Packet packet)
        {
            if (packet.ReadByte() != chr.PortalCount) return;

            var movePath = new MovePath();
            movePath.DecodeFromPacket(packet, MovePath.MovementSource.Player);
            chr.TryTraceMovement(movePath);

            if (chr.AssertForHack(movePath.Elements.Length == 0, "Received Empty Move Path"))
            {
                return;
            }

            var allowed = PacketHelper.ValidateMovePath(chr, movePath, packet.PacketCreationTime);
            if (!allowed && !chr.IsGM)
            {
                //this.Session.Socket.Shutdown(System.Net.Sockets.SocketShutdown.Both);
                //return;
                // TODO: Update speed of character
                // Program.MainForm.LogAppendFormat("Move incorrect: {0}", chr.Name);
            }

            SendPlayerMove(chr, movePath);

            if (!chr.Field.ReallyOutOfBounds.Contains(chr.Position.X, chr.Position.Y))
            {
                if (chr.OutOfMBRCount++ > 5)
                {
                    // Okay, reset.
                    chr.ChangeMap(chr.MapID, chr.Field.GetClosestStartPoint(chr.Position));
                    chr.OutOfMBRCount = 0;
                }
            }
            else
            {
                chr.OutOfMBRCount = 0;
            }
        }

        public static void OnContiMoveState(Character chr, Packet packet)
        {
            var mapid = packet.ReadInt();

            var p = new Packet(ServerMessages.CONTISTATE);
            p.WriteByte((byte)ContinentMan.Instance.GetInfo(mapid, 0));
            p.WriteByte((byte)ContinentMan.Instance.GetInfo(mapid, 1));
            chr.SendPacket(p);
        }

        public static void OnEnterScriptedPortal(Packet packet, Character chr)
        {
            chr.ExclRequestSet = true;

            var portalname = packet.ReadString();

            if (!chr.Field.Portals.TryGetValue(portalname, out var portal)) return;


            var pos = new Pos(portal.X, portal.Y);
            var dist = chr.Position - pos;
            if (chr.AssertForHack(dist > 300, "Portal distance hack (" + dist + ")", dist > 600))
            {
                return;
            }

            if (portal.Enabled == false)
            {
                Program.MainForm.LogDebug(chr.Name + " tried to enter a disabled portal.");
                BlockedMessage(chr, PortalBlockedMessage.ClosedForNow);
                return;
            }

            if (chr.Field.PortalsOpen == false)
            {
                Program.MainForm.LogDebug(chr.Name + " tried to enter a disabled portal.");
                BlockedMessage(chr, PortalBlockedMessage.ClosedForNow);
                return;
            }

            if (!string.IsNullOrEmpty(portal.Script))
            {
                if (!NpcPacket.StartScript(chr, portal.Script, portal.ID))
                {
                    BlockedMessage(chr, PortalBlockedMessage.ClosedForNow);
                }
            }
        }

        public static void OnEnterPortal(Packet packet, Character chr)
        {
            chr.ExclRequestSet = true;

            if (packet.ReadByte() != chr.PortalCount)
            {
                return;
            }

            var opcode = packet.ReadInt();
            var portalname = packet.ReadString();
            if (portalname.Length > 0)
            {
                new Pos(packet);
            }

            packet.ReadByte(); // Related to teleporting to party member? Always 0
            var reviveInCurrentMap = packet.ReadBool();

            switch (opcode)
            {
                case 0:
                    {
                        if (chr.PrimaryStats.HP == 0)
                        {
                            chr.HandleDeath(reviveInCurrentMap);
                        }
                        else if (!chr.IsGM)
                        {
                            Program.MainForm.LogAppend($"Not handling death of {chr.ID}, because user is not dead. Killing him again. HP: " + chr.PrimaryStats.HP);
                            // Kill him anyway
                            chr.DamageHP(30000);
                        }
                        else
                        {
                            // Admin /map 0
                            chr.ChangeMap(opcode);
                        }

                        break;
                    }
                case -1:
                    {
                        if (chr.Field.Portals.TryGetValue(portalname, out var portal) &&
                            MapProvider.Maps.TryGetValue(portal.ToMapID, out var toMap) &&
                            toMap.Portals.TryGetValue(portal.ToName, out var to))
                        {
                            var pos = new Pos(portal.X, portal.Y);
                            var dist = chr.Position - pos;
                            if (chr.AssertForHack(dist > 300, "Portal distance hack (" + dist + ")", dist > 600))
                            {
                                return;
                            }

                            if (portal.Enabled == false)
                            {
                                Program.MainForm.LogDebug(chr.Name + " tried to enter a disabled portal.");
                                BlockedMessage(chr, PortalBlockedMessage.ClosedForNow);
                                return;
                            }

                            if (chr.Field.PortalsOpen == false)
                            {
                                Program.MainForm.LogDebug(chr.Name + " tried to enter a disabled portal.");
                                BlockedMessage(chr, PortalBlockedMessage.ClosedForNow);
                                return;
                            }

                            if (!string.IsNullOrEmpty(portal.Script))
                            {
                                if (!NpcPacket.StartScript(chr, portal.Script))
                                {
                                    BlockedMessage(chr, PortalBlockedMessage.ClosedForNow);
                                }

                                return;
                            }

                            chr.ChangeMap(portal.ToMapID, to);
                        }
                        else
                        {
                            Program.MainForm.LogDebug(chr.Name + " tried to enter unknown portal??? " + portalname + ", " + chr.Field.ID);
                            BlockedMessage(chr, PortalBlockedMessage.ClosedForNow);
                        }


                        break;
                    }
                default:
                    {
                        if (chr.IsGM)
                        {
                            chr.ChangeMap(opcode);
                        }

                        break;
                    }
            }
        }

        public static void HandleSitChair(Character chr, Packet packet)
        {
            chr.ExclRequestSet = true;

            var chair = packet.ReadShort();

            if (chair == -1)
            {
                if (chr.MapChair != -1)
                {
                    chr.Field.UsedSeats.Remove(chr.MapChair);
                    chr.MapChair = -1;
                    SendCharacterSit(chr, -1);
                }
            }
            else
            {
                if (chr.Field != null && chr.Field.Seats.ContainsKey(chair) && !chr.Field.UsedSeats.Contains(chair))
                {
                    chr.Field.UsedSeats.Add(chair);
                    chr.MapChair = chair;
                    SendCharacterSit(chr, chair);
                }
            }
        }


        public static void SendWeatherEffect(Map map, Character victim = null)
        {
            var pw = new Packet(ServerMessages.BLOW_WEATHER);
            pw.WriteBool(map.WeatherIsAdmin);
            pw.WriteInt(map.WeatherID);
            if (!map.WeatherIsAdmin)
                pw.WriteString(map.WeatherMessage);

            if (victim != null)
                victim.SendPacket(pw);
            else
                map.SendPacket(pw);
        }

        public static void SendPlayerMove(Character chr, MovePath movePath)
        {
            var pw = new Packet(ServerMessages.MOVE_PLAYER);
            pw.WriteInt(chr.ID);
            movePath.EncodeToPacket(pw);

            chr.Field.SendPacket(chr, pw, chr);
        }

        public static void SendChatMessage(Character who, LocalizedString message)
        {
            var pw = new Packet(ServerMessages.CHAT);
            pw.WriteInt(who.ID);
            pw.WriteBool(who.IsGM && !who.Undercover);
            pw.WriteString(message);

            who.Field.SendPacket(who, pw);
        }

        public static void SendEmotion(Character chr, int emotion)
        {
            var pw = new Packet(ServerMessages.FACIAL_EXPRESSION);
            pw.WriteInt(chr.ID);
            pw.WriteInt(emotion);

            chr.Field.SendPacket(chr, pw, chr);
        }

        public static void SendCharacterLeavePacket(int id, Character victim)
        {
            var pw = new Packet(ServerMessages.USER_LEAVE_FIELD);
            pw.WriteInt(id);
            victim.SendPacket(pw);
        }

        public static void SendCharacterSit(Character chr, short chairid)
        {
            var pw = new Packet(ServerMessages.SHOW_CHAIR);
            pw.WriteBool(chairid != -1);
            if (chairid != -1)
            {
                pw.WriteShort(chairid);
            }

            chr.SendPacket(pw);
        }

        public static void SendBossHPBar(Map pField, int pHP, int pMaxHP, uint pColorBottom, uint pColorTop)
        {
            pField.SendPacket(GetBossHPBarPacket(pHP, pMaxHP, pColorBottom, pColorTop));
        }

        public static void SendBossHPBarToAdmins(Map pField, int pHP, int pMaxHP, uint pColorBottom, uint pColorTop)
        {
            var pw = GetBossHPBarPacket(pHP, pMaxHP, pColorBottom, pColorTop);
            pField.Characters.Where(x => x.IsGM).ForEach(x => x.SendPacket(pw));
        }

        private static Packet GetBossHPBarPacket(int pHP, int pMaxHP, uint pColorBottom, uint pColorTop)
        {
            var pw = new Packet(ServerMessages.FIELD_EFFECT);
            pw.WriteByte(FieldEffect.MobHPTag);
            pw.WriteInt(pHP);
            pw.WriteInt(pMaxHP);
            pw.WriteUInt(pColorTop);
            pw.WriteUInt(pColorBottom);
            return pw;
        }

        public static void EffectScreen(Character chr, string effect)
        {
            var pw = new Packet(ServerMessages.FIELD_EFFECT);
            pw.WriteByte(FieldEffect.Screen);
            pw.WriteString(effect);
            chr.SendPacket(pw);
        }

        public static void EffectSound(Character chr, string sound)
        {
            var pw = new Packet(ServerMessages.FIELD_EFFECT);
            pw.WriteByte(FieldEffect.Sound);
            pw.WriteString(sound);
            chr.SendPacket(pw);
        }
        public static void EffectChangeBGM(Map field, string BGM)
        {
            var pw = new Packet(ServerMessages.FIELD_EFFECT);
            pw.WriteByte(FieldEffect.ChangeBGM);
            pw.WriteString(BGM);
            field.SendPacket(pw);
        }

        public static void MapEffect(Character chr, byte type, string message, bool ToTeam)
        {
            //Sounds : Party1/Clear // Party1/Failed
            //Messages : quest/party/clear // quest/party/wrong_kor
            var pw = new Packet(ServerMessages.FIELD_EFFECT);
            pw.WriteByte(type); //4: sound 3: message
            pw.WriteString(message);
            if (!ToTeam)
            {
                chr.Field.SendPacket(pw);
            }
            else
            {
                chr.SendPacket(pw);
            }
        }

        public static void PortalEffect(Map field, byte what, string objectName)
        {
            var pw = new Packet(ServerMessages.FIELD_EFFECT);
            pw.WriteByte(FieldEffect.Object);
            pw.WriteByte(what); // Unread
            pw.WriteString(objectName); // Such as 'gate'
            field.SendPacket(pw);
        }


        public static void SpawnMessageBox(MessageBox messageBox)
        {
            var pw = new Packet(ServerMessages.MESSAGE_BOX_ENTER_FIELD);
            pw.WriteInt(messageBox.SN);
            pw.WriteInt(messageBox.ItemID);
            pw.WriteString(messageBox.Message);
            pw.WriteString(messageBox.Creator);
            pw.WriteShort(messageBox.X);
            pw.WriteShort(messageBox.Y);
            messageBox.Field.SendPacket(messageBox, pw);
        }

        public static void DespawnMessageBox(MessageBox messageBox, byte LeaveType)
        {
            var pw = new Packet(ServerMessages.MESSAGE_BOX_LEAVE_FIELD);
            pw.WriteByte(LeaveType);
            pw.WriteInt(messageBox.SN);
            messageBox.Field.SendPacket(messageBox, pw);
        }

        public static void ShowMessageBoxCreateFailed(Character chr)
        {
            //Can't fly it here
            var pw = new Packet(ServerMessages.MESSAGE_BOX_CREATE_FAILED);
            pw.WriteByte(0);
            chr.SendPacket(pw);
        }

        private static Packet GetMapTimerPacket(TimeSpan time)
        {
            var pw = new Packet(ServerMessages.CLOCK);
            pw.WriteByte(0x02);
            pw.WriteInt((int)time.TotalSeconds);
            return pw;
        }

        public static void ShowMapTimerForCharacter(Character chr, int time) => ShowMapTimerForCharacter(chr, TimeSpan.FromSeconds(time));
        public static void ShowMapTimerForCharacter(Character chr, TimeSpan time) => chr.SendPacket(GetMapTimerPacket(time));

        public static void ShowMapTimerForCharacters(IEnumerable<Character> chrs, TimeSpan time)
        {
            var packet = GetMapTimerPacket(time);
            chrs.ForEach(x => x.SendPacket(packet));
        }

        public static void ShowMapTimerForMap(Map map, int time) => ShowMapTimerForMap(map, TimeSpan.FromSeconds(time));
        public static void ShowMapTimerForMap(Map map, TimeSpan time) => map.SendPacket(GetMapTimerPacket(time));

        public static void SendGMEventInstructions(Map map)
        {
            var pw = new Packet(ServerMessages.DESC);
            pw.WriteByte(0x00);
            map.SendPacket(pw);
        }

        public static void SendMapClock(Character chr, int hour, int minute, int second)
        {
            var pw = new Packet(ServerMessages.CLOCK);
            pw.WriteByte(0x01);
            pw.WriteByte((byte)hour);
            pw.WriteByte((byte)minute);
            pw.WriteByte((byte)second);
            chr.SendPacket(pw);
        }

        public static void SendJukebox(Map map, Character victim)
        {
            var pw = new Packet(ServerMessages.PLAY_JUKE_BOX);
            pw.WriteInt(map.JukeboxID);
            if (map.JukeboxID != -1)
                pw.WriteString(map.JukeboxUser);

            if (victim != null)
                victim.SendPacket(pw);
            else
                map.SendPacket(pw);
        }

        public enum PortalBlockedMessage
        {
            ClosedForNow = 1,
            CannotGoToThatPlace = 2
        }

        public static void BlockedMessage(Character chr, PortalBlockedMessage msg)
        {
            var pw = new Packet(ServerMessages.TRANSFER_FIELD_REQ_IGNORED);
            pw.WriteByte((byte)msg);
            chr.SendPacket(pw);
        }


        public static void SendPinkText(Character chr, string text) //needs work 
        {
            var pw = new Packet(ServerMessages.GROUP_MESSAGE);
            pw.WriteByte(1);
            pw.WriteString(chr.VisibleName);
            pw.WriteString(text);
            chr.SendPacket(pw);
        }

        public static void SendCharacterEnterPacket(Character player, Character victim)
        {
            var pw = new Packet(ServerMessages.USER_ENTER_FIELD);

            pw.WriteInt(player.ID);

            pw.WriteString(player.VisibleName);

            BuffPacket.AddMapBuffValues(player, pw);

            PacketHelper.AddAvatar(pw, player);

            pw.WriteInt(player.Inventory.GetEquippedItemId(Constants.EquipSlots.Slots.PetEquip1, true));
            pw.WriteInt(player.Inventory.ActiveItemID);
            pw.WriteInt(player.Inventory.ChocoCount);
            pw.WriteShort(player.Position.X);
            pw.WriteShort(player.Position.Y);
            pw.WriteByte(player.MoveAction);
            pw.WriteShort(player.Foothold);
            pw.WriteBool(player.IsGM && !player.Undercover);

            var petItem = player.GetSpawnedPet();
            pw.WriteBool(petItem != null);
            if (petItem != null)
            {
                pw.WriteInt(petItem.ItemID);
                pw.WriteString(petItem.Name);
                pw.WriteLong(petItem.CashId);
                var ml = petItem.MovableLife;
                pw.WriteShort(ml.Position.X);
                pw.WriteShort(ml.Position.Y);
                pw.WriteByte(ml.MoveAction);
                pw.WriteShort(ml.Foothold);
            }

            // Mini Game & Player Shops
            player.EncodeMiniRoomBalloon(pw);

            //Rings
            pw.WriteByte(0); // Number of Rings, hardcoded 0 until implemented.

            //Ring packet structure
            /**
            for (Ring ring in player.Rings()) {
                pw.WriteLong(ring.getRingId()); // R
                pw.WriteLong(ring.getPartnerRingId());
                pw.WriteInt(ring.getItemId());
            }
            */

            player.Field.EncodeFieldSpecificData(player, pw);

            victim.SendPacket(pw);
        }

        public static void SendPlayerInfo(Character chr, Packet packet)
        {
            var id = packet.ReadInt();
            var victim = chr.Field.GetPlayer(id);
            if (victim == null)
            {
                InventoryPacket.NoChange(chr);
                return;
            }

            var pw = new Packet(ServerMessages.CHARACTER_INFO); // Idk why this is in mappacket, it's part of CWvsContext
            pw.WriteInt(victim.ID);
            pw.WriteByte(victim.PrimaryStats.Level);
            pw.WriteShort(victim.PrimaryStats.Job);
            pw.WriteShort(victim.PrimaryStats.Fame);

            if (chr.IsGM && !victim.IsGM)
                pw.WriteString($"{id}:{victim.UserID}");
            else if (victim.IsGM && !victim.Undercover)
                pw.WriteString("Administrator");
            else
                pw.WriteString($"{victim.MonsterBook.Cards.Sum(pair => pair.Value)} cards");

            var petItem = victim.GetSpawnedPet();
            pw.WriteBool(petItem != null);
            if (petItem != null)
            {
                pw.WriteInt(petItem.ItemID);
                pw.WriteString(petItem.Name);
                pw.WriteByte(petItem.Level);
                pw.WriteShort(petItem.Closeness);
                pw.WriteByte(petItem.Fullness);
                pw.WriteInt(victim.Inventory.GetEquippedItemId(Constants.EquipSlots.Slots.PetEquip1, true)); // Pet equip.
            }

            pw.WriteByte((byte)victim.Wishlist.Count);
            victim.Wishlist.ForEach(pw.WriteInt);

            // TODO: Read and display data
            victim.Inventory.GenerateVisibleEquipsPacket(pw);

            chr.SendPacket(pw);
        }

        public static void SendAvatarModified(Character chr, AvatarModFlag AvatarModFlag = 0)
        {
            var pw = new Packet(ServerMessages.AVATAR_MODIFIED);
            pw.WriteInt(chr.ID);
            pw.WriteInt((int)AvatarModFlag);

            if ((AvatarModFlag & AvatarModFlag.Skin) == AvatarModFlag.Skin)
                pw.WriteByte(chr.Skin);
            if ((AvatarModFlag & AvatarModFlag.Face) == AvatarModFlag.Face)
                pw.WriteInt(chr.Face);

            pw.WriteBool((AvatarModFlag & AvatarModFlag.Equips) == AvatarModFlag.Equips);
            if ((AvatarModFlag & AvatarModFlag.Equips) == AvatarModFlag.Equips)
            {
                pw.WriteByte(0); //My Hair is a Bird, Your Argument is Invalid
                pw.WriteInt(chr.Hair);
                chr.Inventory.GeneratePlayerPacket(pw);
                pw.WriteByte(0xFF); // Equips shown end
                pw.WriteInt(chr.Inventory.GetEquippedItemId(Constants.EquipSlots.Slots.Weapon, true));
                pw.WriteInt(chr.Inventory.GetEquippedItemId(Constants.EquipSlots.Slots.PetEquip1, true));
            }

            pw.WriteBool((AvatarModFlag & AvatarModFlag.ItemEffects) == AvatarModFlag.ItemEffects);
            if ((AvatarModFlag & AvatarModFlag.ItemEffects) == AvatarModFlag.ItemEffects)
            {
                pw.WriteInt(chr.Inventory.ActiveItemID);
                pw.WriteInt(chr.Inventory.ChocoCount);
            }

            pw.WriteBool((AvatarModFlag & AvatarModFlag.Speed) == AvatarModFlag.Speed);
            if ((AvatarModFlag & AvatarModFlag.Speed) == AvatarModFlag.Speed)
                pw.WriteByte(chr.PrimaryStats.TotalSpeed);

            pw.WriteBool((AvatarModFlag & AvatarModFlag.Rings) == AvatarModFlag.Rings);
            if ((AvatarModFlag & AvatarModFlag.Rings) == AvatarModFlag.Rings)
            {
                pw.WriteLong(0);
                pw.WriteLong(0);
            }

            chr.Field.SendPacket(chr, pw, chr);

            if (chr.IsInMiniRoom)
            {
                // Make sure we update the look in the room as well.
                chr.RoomV2?.OnAvatarChanged(chr);
            }
        }

        [Flags]
        public enum PlayerEffectTargets
        {
            ToMap = 1,
            ToPlayer = 2,

            ToPlayerAndMap = ToMap | ToPlayer,
        }

        public static void SendPlayerEffect(Character chr, PlayerEffectTargets targets, UserEffect effect, Action<Packet> extraData = null)
        {
            if (targets.HasFlag(PlayerEffectTargets.ToPlayer))
            {
                var pw = new Packet(ServerMessages.LOCAL_USER_EFFECT);
                pw.WriteByte(effect);
                extraData?.Invoke(pw);
                chr.SendPacket(pw);
            }

            if (targets.HasFlag(PlayerEffectTargets.ToMap))
            {
                var pw = new Packet(ServerMessages.REMOTE_USER_EFFECT);
                pw.WriteInt(chr.ID);
                pw.WriteByte(effect);
                extraData?.Invoke(pw);
                chr.Field.SendPacket(chr, pw, chr);
            }
        }

        public static void PlayPortalSE(Character chr)
        {
            SendPlayerEffect(chr, PlayerEffectTargets.ToPlayer, UserEffect.PlayPortalSE);
        }

        public static void SendPlayerLevelupAnim(Character chr)
        {
            SendPlayerEffect(chr, PlayerEffectTargets.ToMap, UserEffect.LevelUp);
        }

        public static void SendGainMonsterBook(Character chr)
        {
            SendPlayerEffect(chr, PlayerEffectTargets.ToPlayerAndMap, UserEffect.MonsterBookCardGet);
        }

        public static void SendJobChangeEffect(Character chr)
        {
            SendPlayerEffect(chr, PlayerEffectTargets.ToPlayerAndMap, UserEffect.JobChanged);
        }

        public static void SendQuestClearEffect(Character chr)
        {
            SendPlayerEffect(chr, PlayerEffectTargets.ToPlayerAndMap, UserEffect.QuestComplete);
        }

        public static void SendScrollResult(Character chr, bool success)
        {
            SendPlayerEffect(chr, PlayerEffectTargets.ToPlayerAndMap, UserEffect.ItemMaker, pw => pw.WriteBool(success));
        }

        // Plays the Special effect node of a skill on a player.
        public static void SendPlayerSpecialSkillAnim(Character chr, int skillid)
        {
            SendPlayerEffect(chr, PlayerEffectTargets.ToPlayerAndMap, UserEffect.SkillSpecial, pw => pw.WriteInt(skillid));
        }

        public static void SendPlayerSkillAnim(Character chr, int skillid, byte level)
        {
            SendPlayerEffect(chr, PlayerEffectTargets.ToMap, UserEffect.SkillAffected, pw => { pw.WriteInt(skillid); pw.WriteByte(level); });
        }

        public static void SendPlayerSkillAnimSelf(Character chr, int skillid, byte level)
        {
            SendPlayerEffect(chr, PlayerEffectTargets.ToPlayer, UserEffect.SkillAffected, pw => { pw.WriteInt(skillid); pw.WriteByte(level); });
        }

        public static void SendPlayerSkillAnimThirdParty(Character chr, int skillid, byte level, bool party, bool self)
        {
            SendPlayerEffect(
                chr,
                party && self ? PlayerEffectTargets.ToPlayer : PlayerEffectTargets.ToMap,
                party ? UserEffect.SkillAffected_Select : UserEffect.SkillAffected,
                pw => { pw.WriteInt(skillid); pw.WriteByte(level); }
            );
        }

        public static void SendPlayerBuffed(Character chr, BuffValueTypes pBuffs, short delay = 0)
        {
            var pw = new Packet(ServerMessages.GIVE_FOREIGN_BUFF);
            pw.WriteInt(chr.ID);
            BuffPacket.AddMapBuffValues(chr, pw, pBuffs);
            pw.WriteShort(delay); // the delay. usually 0, but is carried on through OnStatChangeByMobSkill / DoActiveSkill_(Admin/Party/Self)StatChange

            chr.Field.SendPacket(chr, pw, chr);
        }

        public static void SendPlayerDebuffed(Character chr, BuffValueTypes buffFlags)
        {
            var pw = new Packet(ServerMessages.RESET_FOREIGN_BUFF);
            pw.WriteInt(chr.ID);
            pw.WriteUInt((uint)((ulong)buffFlags >> 32));
            pw.WriteUInt((uint)((ulong)buffFlags & uint.MaxValue));

            chr.Field.SendPacket(chr, pw, chr);
        }

        public static void SendChangeMap(Character chr)
        {
            chr.ExclRequestSet = false;
            var pack = new Packet(ServerMessages.SET_FIELD);

            WriteCFGForcedPosition(chr, pack);

            pack.WriteInt(Server.Instance.ID); // Channel ID
            pack.WriteByte(chr.PortalCount);
            pack.WriteBool(false); // Is not connecting
            pack.WriteInt(chr.MapID);
            pack.WriteByte(chr.MapPosition);
            pack.WriteShort(chr.PrimaryStats.HP);
            chr.SendPacket(pack);
        }

        private static void WriteCFGForcedPosition(Character chr, Packet pack)
        {
            if (pack.WriteBool(chr.ForcedLocation))
            {
                pack.WriteShort(chr.Position.X);
                pack.WriteShort(chr.Position.Y);
                // Only send it once
                chr.ForcedLocation = false;
            }
        }

        public static void SendJoinGame(Character chr)
        {
            chr.ExclRequestSet = false;
            var pack = new Packet(ServerMessages.SET_FIELD);

            WriteCFGForcedPosition(chr, pack);

            pack.WriteInt(Server.Instance.ID); // Channel ID
            pack.WriteByte(chr.PortalCount);
            pack.WriteBool(true); // Is connecting

            {
                var rnd = Server.Instance.Randomizer;
                // Seeds are initialized by global randomizer
                var seed1 = rnd.Random();
                var seed2 = rnd.Random();
                var seed3 = rnd.Random();
                var seed4 = rnd.Random();

                chr.CalcDamageRandomizer.SetSeed(seed1, seed2, seed3);
                chr.RndActionRandomizer.SetSeed(seed2, seed3, seed4);

                pack.WriteUInt(seed1);
                pack.WriteUInt(seed2);
                pack.WriteUInt(seed3);
                pack.WriteUInt(seed4);
            }

            pack.WriteShort(-1); // Flags (contains everything: 0xFFFF)

            pack.WriteInt(chr.ID);
            pack.WriteString(chr.VisibleName, 13);
            pack.WriteByte(chr.Gender); // Gender
            pack.WriteByte(chr.Skin); // Skin
            pack.WriteInt(chr.Face); // Face
            pack.WriteInt(chr.Hair); // Hair

            pack.WriteLong(chr.PetCashId); // Pet Cash ID :/

            pack.WriteByte(chr.PrimaryStats.Level);
            pack.WriteShort(chr.PrimaryStats.Job);
            pack.WriteShort(chr.PrimaryStats.Str);
            pack.WriteShort(chr.PrimaryStats.Dex);
            pack.WriteShort(chr.PrimaryStats.Int);
            pack.WriteShort(chr.PrimaryStats.Luk);
            pack.WriteShort(chr.PrimaryStats.HP);
            pack.WriteShort(chr.PrimaryStats.GetMaxHP(true));
            pack.WriteShort(chr.PrimaryStats.MP);
            pack.WriteShort(chr.PrimaryStats.GetMaxMP(true));
            pack.WriteShort(chr.PrimaryStats.AP);
            pack.WriteShort(chr.PrimaryStats.SP);
            pack.WriteInt(chr.PrimaryStats.EXP);
            pack.WriteShort(chr.PrimaryStats.Fame);

            pack.WriteInt(chr.MapID); // Mapid
            pack.WriteByte(chr.MapPosition); // Mappos

            pack.WriteLong(0);
            pack.WriteInt(0);
            pack.WriteInt(0);

            pack.WriteByte((byte)chr.PrimaryStats.BuddyListCapacity); // Buddylist slots

            chr.Inventory.GenerateInventoryPacket(pack);

            chr.Skills.AddSkills(pack);


            var questsWithData = chr.Quests.Quests;
            pack.WriteShort((short)questsWithData.Count); // Running quests
            foreach (var kvp in questsWithData)
            {
                pack.WriteInt(kvp.Key);
                pack.WriteString(kvp.Value.Data);
            }

            pack.WriteShort(2); // Games
            var gs = chr.GameStats;
            {
                pack.WriteInt((int)MiniRoomBase.E_MINI_ROOM_TYPE.MR_MemoryGameRoom);
                pack.WriteInt(gs.MatchCardWins);
                pack.WriteInt(gs.MatchCardTies);
                pack.WriteInt(gs.MatchCardLosses);
                pack.WriteInt(gs.MatchCardScore);
            }
            {
                pack.WriteInt((int)MiniRoomBase.E_MINI_ROOM_TYPE.MR_OmokRoom);
                pack.WriteInt(gs.OmokWins);
                pack.WriteInt(gs.OmokTies);
                pack.WriteInt(gs.OmokLosses);
                pack.WriteInt(gs.OmokScore);
            }


            pack.WriteShort(0);
            /*
             * For every ring, 33 unknown bytes.
            */


            chr.Inventory.AddRockPacket(pack);

            chr.SendPacket(pack);
        }

        public static void CancelSkillEffect(Character chr, int skillid)
        {
            var pw = new Packet(ServerMessages.SKILL_END);
            pw.WriteInt(chr.ID);
            pw.WriteInt(skillid);
            chr.Field.SendPacket(pw, chr);
        }

        public static void SetTownPortalDataOwner(Character chr, MysticDoor door)
        {
            var pw = new Packet(ServerMessages.TOWN_PORTAL);

            // Note: the two shorts will not be read in case of a DefaultNoDoor. Not a big issue
            (door ?? MysticDoor.DefaultNoDoor).Encode(pw);

            chr.SendPacket(pw);
        }
        
        public static Packet ShowDoor(MysticDoor door, byte enterType)
        {
            var pw = new Packet(ServerMessages.TOWN_PORTAL_CREATED);
            pw.WriteByte(enterType); //Does this decide if the animation plays when it is shown?
            pw.WriteInt(door.OwnerId);
            pw.WriteShort(door.X);
            pw.WriteShort(door.Y);

            Trace.WriteLine($"Spawning Door @ {door.X} {door.Y}, owner {door.OwnerId}");

            return pw;
        }

        public static Packet RemoveDoor(MysticDoor door, byte leaveType)
        {
            var pw = new Packet(ServerMessages.TOWN_PORTAL_REMOVED);
            pw.WriteByte(leaveType);
            pw.WriteInt(door.OwnerId);
            return pw;
        }

        public static void HandleDoorUse(Character chr, Packet packet)
        {
            var doorOwnerCharacterID = packet.ReadInt();
            var doorOwnerPartyMemberIdx = PartyData.GetMemberIdx(doorOwnerCharacterID) ?? 0;

            var enterFromTown = packet.ReadBool();
            var doorsToCheck = enterFromTown ? chr.Field.DoorPool.DoorsLeadingHere : chr.Field.DoorPool.Doors;

            if (doorsToCheck.TryGetValue(doorOwnerCharacterID, out var door) && door.CanEnterDoor(chr))
            {
                chr.ChangeMap(enterFromTown ? door.FieldID : door.TownID, doorOwnerPartyMemberIdx, door);
            }

            InventoryPacket.NoChange(chr);
        }

        public static Packet ShowSummon(Summon summon, byte enterType)
        {
            var pw = new Packet(ServerMessages.SPAWN_ENTER_FIELD);
            pw.WriteInt(summon.OwnerId);
            pw.WriteInt(summon.SkillId);
            pw.WriteByte(summon.SkillLevel);
            pw.WriteShort(summon.Position.X);
            pw.WriteShort(summon.Position.Y);
            pw.WriteByte(summon.MoveAction);
            pw.WriteUShort(summon.FootholdSN);

            if (summon is Puppet p)
            {
                pw.WriteByte(0); //entertype 1 is broken for puppet in v12, idk why
                pw.WriteByte(0);
                pw.WriteByte(0);
            }
            else
            {
                pw.WriteByte(enterType);
                pw.WriteLong(0); //bMoveability? bassist?
            }

            return pw;
        }

        public static Packet RemoveSummon(Summon summon, byte leaveType)
        {
            var pw = new Packet(ServerMessages.SPAWN_LEAVE_FIELD);
            pw.WriteInt(summon.OwnerId);
            pw.WriteInt(summon.SkillId);
            pw.WriteByte(leaveType);
            return pw;
        }

        public static void HandleSummonMove(Character chr, Packet packet)
        {
            if (packet.ReadByte() != chr.PortalCount)
            {
                return;
            }

            var skillId = packet.ReadInt();

            if (!chr.Summons.GetSummon(skillId, out var summon)) return;

            var movePath = new MovePath();
            movePath.DecodeFromPacket(packet, MovePath.MovementSource.Summon);
            chr.TryTraceMovement(movePath);

            PacketHelper.ValidateMovePath(summon, movePath, packet.PacketCreationTime);

            SendMoveSummon(chr, summon, movePath);
        }

        private static void SendMoveSummon(Character chr, Summon summon, MovePath movePath)
        {
            var pw = new Packet(ServerMessages.SPAWN_MOVE);
            pw.WriteInt(chr.ID);
            pw.WriteInt(summon.SkillId);
            movePath.EncodeToPacket(pw);

            chr.Field.SendPacket(pw, chr);
        }

        public static void HandleSummonDamage(Character chr, Packet packet)
        {
            var summonid = packet.ReadInt();
            if (!chr.Summons.GetSummon(summonid, out var summon)) return;
            if (!(summon is Puppet puppet)) return;

            var unk = packet.ReadSByte();
            var damage = packet.ReadInt();
            var mobid = packet.ReadInt();
            var unk2 = packet.ReadByte();

            SendDamageSummon(chr, puppet, unk, damage, mobid, unk2);

            puppet.TakeDamage(damage);
        }

        private static void SendDamageSummon(Character chr, Puppet summon, sbyte unk, int damage, int mobid, byte unk2)
        {
            // Needs to be fixed.
            var pw = new Packet(ServerMessages.SPAWN_HIT);
            pw.WriteInt(chr.ID);
            pw.WriteInt(summon.SkillId);
            pw.WriteSByte(-1);
            pw.WriteInt(damage);
            pw.WriteInt(mobid);

            pw.WriteByte(0);
            pw.WriteLong(0);
            pw.WriteLong(0);
            pw.WriteLong(0);
            chr.Field.SendPacket(pw, chr);
        }
    }
}