using System;
using System.Collections.Generic;
using log4net;
using WvsBeta.Common;
using WvsBeta.Game.GameObjects;
using WvsBeta.Game.Packets;
using WvsBeta.SharedDataProvider.Templates;

namespace WvsBeta.Game
{
    public partial class Character
    {
        private static ILog _levelLog = LogManager.GetLogger("LevelLog");

        // Do not remove: is used by NPCs
        public byte GetGender() => Gender;

        public void SetJob(short value)
        {
            if (!DataProvider.Jobs.ContainsKey(value))
            {
                _characterLog.Error($"Set job to invalid value: {value}, setting to beginner.");
                value = 0;
            }

            _characterLog.Info(new StatChangeLogRecord { value = value, type = "job", add = false });
            PrimaryStats.Job = value;
            CharacterStatsPacket.SendUpdateStat(this, StatFlags.Job);
            Guild?.UpdatePlayer(this);
            MapPacket.SendJobChangeEffect(this);

            this.FlushDamageLog();
            Server.Instance.CenterConnection.UpdatePlayerJobLevel(this);
        }

        public void SetEXP(int value)
        {
            PrimaryStats.EXP = value;
            CharacterStatsPacket.SendUpdateStat(this, StatFlags.Exp);
        }

        public void SetHPAndMaxHP(short value, bool sendPacket = true)
        {
            if (value <= 0)
            {
                value = 1;
            }

            SetMaxHP(value);
            PrimaryStats.HP = value;

            if (sendPacket == true)
            {
                CharacterStatsPacket.SendUpdateStat(this, StatFlags.Hp);
            }
        }

        private static readonly List<(int itemID, string msg)> _expLossPreventionItems = new List<(int itemID, string msg)>
        {
            (4140100, "No EXP was lost thanks to the magic of the Heart Chocolate."),
            (4140200, "No EXP was lost thanks to the magic of the Chocolate Basket."),
            (4120001, "No EXP was lost thanks to the magic of the Easter Charm."),
            (4120000, "The EXP did not drop after using the Safety Charm once."),
        };

        public void ModifyHP(short value, bool sendPacket = true)
        {
            var startValue = PrimaryStats.HP;

            PrimaryStats.HP = CapStatShort(PrimaryStats.HP, value, 0, PrimaryStats.GetMaxHP(false));
            
            if (startValue == PrimaryStats.HP)
            {
                // Doesn't matter
                return;
            }

            if (sendPacket)
            {
                CharacterStatsPacket.SendUpdateStat(this, StatFlags.Hp);
            }

            if (PrimaryStats.HP == 0)
            {
                // You died.

                var loseEXP = true;
                foreach (var (itemID, msg) in _expLossPreventionItems)
                {
                    var itemCount = Inventory.ItemCount(itemID);
                    if (itemCount <= 0) continue;

                    Inventory.TakeItem(itemID, 1);
                    itemCount -= 1;

                    MessagePacket.SendTextPlayer(MessagePacket.MessageTypes.RedText, $"{msg} ({itemCount} left)", this, true);

                    loseEXP = false;
                    break;
                }
                
                if (loseEXP)
                {
                    LoseEXP();
                }

                PrimaryStats.Reset(true);
                Summons.RemoveAllSummons();

                RoomV2?.OnUserLeave(this);
                RoomV2 = null;
            }

        }

        public void DamageHP(short amount) => ModifyHP((short)-amount);

        public void SetMPAndMaxMP(short value, bool sendPacket = true)
        {
            if (value < 0)
            {
                value = 0;
            }

            SetMaxMP(value);
            PrimaryStats.MP = value;

            if (sendPacket == true)
            {
                CharacterStatsPacket.SendUpdateStat(this, StatFlags.Mp);
            }
        }

        public void ModifyMP(short value, bool sendPacket = true)
        {
            PrimaryStats.MP = CapStatShort(PrimaryStats.MP, value, 0, PrimaryStats.GetMaxMP(false));

            if (sendPacket)
            {
                CharacterStatsPacket.SendUpdateStat(this, StatFlags.Mp);
            }
        }

        public short CapStatShort(short currentStat, short modification, short min, short max)
        {
            long newStat = currentStat + modification;
            if (newStat > max) newStat = max;
            else if (newStat < min) newStat = min;
            return (short)newStat;
        }
        public int CapStatInt(long currentStat, long modification, long min, long max)
        {
            long newStat = currentStat + modification;
            if (newStat > max) newStat = max;
            else if (newStat < min) newStat = min;
            return (int)newStat;
        }


        public void DamageMP(short amount)
        {
            PrimaryStats.MP = CapStatShort(PrimaryStats.MP, (short)-amount, 0, PrimaryStats.MaxMP);
            CharacterStatsPacket.SendUpdateStat(this, StatFlags.Mp);
        }

        public void ModifyMaxMP(short value)
        {
            value = CapStatShort(PrimaryStats.MaxMP, value, Constants.MinMaxMp, Constants.MaxMaxMp);
            _characterLog.Info(new StatChangeLogRecord { value = value - PrimaryStats.MaxMP, type = "maxmp", add = true });
            PrimaryStats.MaxMP = value;

            CharacterStatsPacket.SendUpdateStat(this, StatFlags.MaxMp);
        }
        public void SetMaxMP(short value)
        {
            value = CapStatShort(0, value, Constants.MinMaxMp, Constants.MaxMaxMp);
            _characterLog.Info(new StatChangeLogRecord { value = value, type = "maxmp", add = false });
            PrimaryStats.MaxMP = value;
            CharacterStatsPacket.SendUpdateStat(this, StatFlags.MaxMp);
        }

        public void ModifyMaxHP(short value)
        {
            value = CapStatShort(PrimaryStats.MaxHP, value, Constants.MinMaxHp, Constants.MaxMaxHp);
            _characterLog.Info(new StatChangeLogRecord { value = value - PrimaryStats.MaxHP, type = "maxhp", add = true });
            PrimaryStats.MaxHP = value;
            CharacterStatsPacket.SendUpdateStat(this, StatFlags.MaxHp);
        }

        public void SetMaxHP(short value)
        {
            value = CapStatShort(0, value, Constants.MinMaxHp, Constants.MaxMaxHp);
            _characterLog.Info(new StatChangeLogRecord { value = value, type = "maxhp", add = false });
            PrimaryStats.MaxHP = value;
            CharacterStatsPacket.SendUpdateStat(this, StatFlags.MaxHp);
        }

        // set setEXP when you want to update EXP too (and update them at the same time).
        public void SetLevel(byte value, int setEXP = -1)
        {
            var flags = StatFlags.Level;
            if (setEXP > -1)
            {
                flags |= StatFlags.Exp;
                PrimaryStats.EXP = setEXP;
            }

            _characterLog.Info(new StatChangeLogRecord { value = value, type = "level", add = false });
            PrimaryStats.Level = value;
            CharacterStatsPacket.SendUpdateStat(this, flags);
            Guild?.UpdatePlayer(this);

            MapPacket.SendPlayerLevelupAnim(this);

            this.FlushDamageLog();
            Server.Instance.CenterConnection.UpdatePlayerJobLevel(this);
        }

        public void AddFame(short value)
        {
            value = CapStatShort(PrimaryStats.Fame, value, Int16.MinValue, Int16.MaxValue);
            _characterLog.Info(new StatChangeLogRecord { value = value - PrimaryStats.Fame, type = "fame", add = true });
            PrimaryStats.Fame = value;
            CharacterStatsPacket.SendUpdateStat(this, StatFlags.Fame);
        }

        public void SetFame(short value)
        {
            value = CapStatShort(0, value, Int16.MinValue, Int16.MaxValue);
            _characterLog.Info(new StatChangeLogRecord { value = value, type = "fame", add = false });
            PrimaryStats.Fame = value;
            CharacterStatsPacket.SendUpdateStat(this, StatFlags.Fame);
        }

        private byte lastSaveStep = 0;

        private static byte GetPercentagePerLevelToSave(byte level)
        {
            if (level >= 50) return 2; // Every 2 percent for lvl 50+ peeps
            // Savepoints per level. High levels (>=25) have every 4%. Low levels have 50, 33, 25, 20, etc..
            return (byte)(100 / Math.Min(level, (byte)25));
        }

        private byte CalculateSaveStep()
        {
            var expRequired = Constants.GetLevelEXP(PrimaryStats.Level);
            if (expRequired == 0) return 0;

            var percentage = (byte)(((ulong)PrimaryStats.EXP * 100) / (ulong)expRequired);

            var percentagePerLevel = GetPercentagePerLevelToSave(PrimaryStats.Level);

            return (byte)(percentage / percentagePerLevel);
        }

        public void AddPoints(int amount)
        {
            var currentPoints = Quests.GetQuestData(1001300, "0");

            Quests.SetQuestData(1001300, (int.Parse(currentPoints) + amount).ToString());
            MessagePacket.SendScrMessage(this, $"You have gained Internet Cafe Points (+{amount})", 0x7);
        }

        public void AddEXP(double value, bool IsLastHit = false, bool Quest = false) => AddEXP((uint)value, IsLastHit, Quest);

        public void AddEXP(uint value, bool IsLastHit = false, bool Quest = false)
        {
            if (value == 0 || PrimaryStats.Level >= 200 || PrimaryStats.HP <= 0) return;

            var amount = (int)(value > Int32.MaxValue ? Int32.MaxValue : value);
            var newEXP = (uint)(PrimaryStats.EXP + amount);
            var amountGained = (uint)0;

            var level = PrimaryStats.Level;

            var save = false;
            var expRequired = Constants.GetLevelEXP(PrimaryStats.Level);
            
            if (newEXP >= expRequired)
            {
                short apgain = 0;
                short spgain = 0;
                short mpgain = 0;
                short hpgain = 0;
                var job = (short)(PrimaryStats.Job / 100);

                var intt = PrimaryStats.GetIntAddition(true);

                amountGained = (uint)(expRequired - PrimaryStats.EXP);

                // Reduce amount left
                newEXP -= (uint)expRequired;
                level++;

                // Update EXP required...
                expRequired = Constants.GetLevelEXP(level);

                if (level >= 200)
                {
                    newEXP = 0;
                    // TODO: Announce max level!
                }

                // Overflow? lets reduce it
                if (newEXP >= expRequired)
                {
                    newEXP = (uint)(expRequired - 1);
                }
                amountGained += newEXP;


                apgain += Constants.ApPerLevel;
                hpgain += (short)Rand32.NextBetween(
                    Constants.HpMpFormulaArguments[job, 0, (int)Constants.HpMpFormulaFields.HPMin],
                    Constants.HpMpFormulaArguments[job, 0, (int)Constants.HpMpFormulaFields.HPMax]
                );

                mpgain += (short)Rand32.NextBetween(
                    Constants.HpMpFormulaArguments[job, 0, (int)Constants.HpMpFormulaFields.MPMin],
                    Constants.HpMpFormulaArguments[job, 0, (int)Constants.HpMpFormulaFields.MPMax]
                );

                // Additional buffing through INT stats
                mpgain += (short)(
                    intt *
                    Constants.HpMpFormulaArguments[job, 0, (int)Constants.HpMpFormulaFields.MPIntStatMultiplier] /
                    200
                );

                var improvedMaxHpIncreaseLvl = Skills.GetSkillLevel(Constants.Swordman.Skills.ImprovedMaxHpIncrease);
                if (improvedMaxHpIncreaseLvl > 0)
                {
                    hpgain += CharacterSkills.GetSkillLevelData(Constants.Swordman.Skills.ImprovedMaxHpIncrease, improvedMaxHpIncreaseLvl).XValue;
                }

                var improvedMaxMpIncreaseLvl = Skills.GetSkillLevel(Constants.Magician.Skills.ImprovedMaxMpIncrease);
                if (improvedMaxMpIncreaseLvl > 0)
                {
                    mpgain += CharacterSkills.GetSkillLevelData(Constants.Magician.Skills.ImprovedMaxMpIncrease, improvedMaxMpIncreaseLvl).XValue;
                }

                if (PrimaryStats.Job != 0)
                {
                    spgain = Constants.SpPerLevel;
                }


                _levelLog.Info(new LevelLogRecord
                {
                    level = level,
                    posX = Position.X,
                    posY = Position.Y,
                });

                ModifyMaxHP(hpgain);
                ModifyMaxMP(mpgain);
                SetLevel(level, (int)newEXP);
                AddAP(apgain);
                AddSP(spgain);
                ModifyHP(PrimaryStats.GetMaxHP(false));
                ModifyMP(PrimaryStats.GetMaxMP(false));
                GiveReferralCash();
                save = true;
            }
            else
            {
                amountGained = (uint) amount;
                SetEXP((int)newEXP);
            }

            CharacterStatsPacket.SendGainEXP(this, (int)amountGained, IsLastHit, Quest);

            // Calculate savepoints

            var stepOfSave = CalculateSaveStep();
            var curDateTime = MasterThread.CurrentDate;
            if (!save)
            {
                if (lastSaveStep != stepOfSave)
                {
                    var levelTimeSpan = curDateTime - LastSavepoint;

                    if (levelTimeSpan.TotalSeconds >= 30)
                    {
                        _characterLog.Debug(
                            $"Saving because user reached save threshold. Current {stepOfSave} last {lastSaveStep}");
                        save = true;
                        LastSavepoint = curDateTime;
                    }
                    else
                    {
                        AssertForHack(
                            levelTimeSpan.TotalSeconds < 20,
                            $"Getting fast EXP ({levelTimeSpan.TotalSeconds} seconds since last savepoint)",
                            levelTimeSpan.TotalSeconds < 15
                        );
                    }
                    _characterLog.Debug(
                        new SavepointLogRecord
                        {
                            level = PrimaryStats.Level,
                            posX = Position.X,
                            posY = Position.Y,
                            totalMillisBetween = (int)levelTimeSpan.TotalMilliseconds,
                            blocked = save == false
                        }
                    );

                    lastSaveStep = stepOfSave;
                }
            }
            else
            {
                lastSaveStep = stepOfSave;
            }


            if (save)
            {
                LastSavepoint = curDateTime;
                Save();
            }
        }

        public void IncreaseBuddySlots(byte slots = 5)
        {
            Server.Instance.CenterConnection.BuddyListExpand(this, slots);
        }

        /// <summary>
        /// Gives 'value' amount of mesos to the character. Overflows are covered.
        /// </summary>
        /// <param name="value">Amount of mesos</param>
        /// <returns>Amount of mesos given</returns>
        public int AddMesos(int value)
        {
            value = CapStatInt(Inventory.Mesos, value, 0, Int32.MaxValue);

            int mesosDiff = value - Inventory.Mesos;
            Inventory.Mesos = value;
            CharacterStatsPacket.SendUpdateStat(this, StatFlags.Mesos);

            return mesosDiff;
        }

        public void SetMesos(int value)
        {
            Inventory.Mesos = CapStatInt(Inventory.Mesos, value, 0, Int32.MaxValue);
            CharacterStatsPacket.SendUpdateStat(this, StatFlags.Mesos);
        }

        public void AddMaplePoints(int value, string note = "")
            => AddPointTransaction(value, "maplepoints", note);

        public void AddCash(int value, string note = "")
            => AddPointTransaction(value, "nx", note);

        private void AddPointTransaction(int value, string type, string note = "")
        {
            Server.Instance.CharacterDatabase.AddPointTransaction(UserID, value, type, note);
        }


        public void AddAP(short value)
        {
            value = CapStatShort(PrimaryStats.AP, value, 0, Constants.MaxStat);
            _characterLog.Info(new StatChangeLogRecord { value = value - PrimaryStats.AP, type = "ap", add = true });

            PrimaryStats.AP = value;
            CharacterStatsPacket.SendUpdateStat(this, StatFlags.Ap);
        }

        public void SetAP(short value)
        {
            value = CapStatShort(0, value, 0, Constants.MaxStat);
            _characterLog.Info(new StatChangeLogRecord { value = value, type = "ap", add = false });

            PrimaryStats.AP = value;
            CharacterStatsPacket.SendUpdateStat(this, StatFlags.Ap);
        }

        public void AddSP(short value)
        {
            value = CapStatShort(PrimaryStats.SP, value, 0, Constants.MaxStat);
            _characterLog.Info(new StatChangeLogRecord { value = value - PrimaryStats.SP, type = "sp", add = true });

            PrimaryStats.SP = value;
            CharacterStatsPacket.SendUpdateStat(this, StatFlags.Sp);
        }

        public void SetSP(short value)
        {
            value = CapStatShort(0, value, 0, Constants.MaxStat);
            _characterLog.Info(new StatChangeLogRecord { value = value, type = "sp", add = false });

            PrimaryStats.SP = value;
            CharacterStatsPacket.SendUpdateStat(this, StatFlags.Sp);
        }

        public void AddStr(short value)
        {
            value = CapStatShort(PrimaryStats.Str, value, 4, Constants.MaxStat);
            _characterLog.Info(new StatChangeLogRecord { value = value - PrimaryStats.Str, type = "str", add = true });

            PrimaryStats.Str = value;
            CharacterStatsPacket.SendUpdateStat(this, StatFlags.Str);

            this.FlushDamageLog();
        }

        public void SetStr(short value)
        {
            value = CapStatShort(0, value, 4, Constants.MaxStat);
            _characterLog.Info(new StatChangeLogRecord { value = value, type = "str", add = false });

            PrimaryStats.Str = value;
            CharacterStatsPacket.SendUpdateStat(this, StatFlags.Str);

            this.FlushDamageLog();
        }

        public void AddDex(short value)
        {
            value = CapStatShort(PrimaryStats.Dex, value, 4, Constants.MaxStat);
            _characterLog.Info(new StatChangeLogRecord { value = value - PrimaryStats.Dex, type = "dex", add = true });

            PrimaryStats.Dex = value;
            CharacterStatsPacket.SendUpdateStat(this, StatFlags.Dex);

            this.FlushDamageLog();
        }

        public void SetDex(short value)
        {
            value = CapStatShort(0, value, 4, Constants.MaxStat);
            _characterLog.Info(new StatChangeLogRecord { value = value, type = "dex", add = false });

            PrimaryStats.Dex = value;
            CharacterStatsPacket.SendUpdateStat(this, StatFlags.Dex);

            this.FlushDamageLog();
        }

        public void AddInt(short value)
        {
            value = CapStatShort(PrimaryStats.Int, value, 4, Constants.MaxStat);
            _characterLog.Info(new StatChangeLogRecord { value = value - PrimaryStats.Int, type = "int", add = true });

            PrimaryStats.Int = value;
            CharacterStatsPacket.SendUpdateStat(this, StatFlags.Int);

            this.FlushDamageLog();
        }

        public void SetInt(short value)
        {
            value = CapStatShort(0, value, 4, Constants.MaxStat);
            _characterLog.Info(new StatChangeLogRecord { value = value, type = "int", add = false });

            PrimaryStats.Int = value;
            CharacterStatsPacket.SendUpdateStat(this, StatFlags.Int);

            this.FlushDamageLog();
        }

        public void AddLuk(short value)
        {
            value = CapStatShort(PrimaryStats.Luk, value, 4, Constants.MaxStat);
            _characterLog.Info(new StatChangeLogRecord { value = value - PrimaryStats.Luk, type = "luk", add = true });

            PrimaryStats.Luk = value;
            CharacterStatsPacket.SendUpdateStat(this, StatFlags.Luk);

            this.FlushDamageLog();
        }

        public void SetLuk(short value)
        {
            value = CapStatShort(0, value, 4, Constants.MaxStat);
            _characterLog.Info(new StatChangeLogRecord { value = value, type = "luk", add = false });

            PrimaryStats.Luk = value;
            CharacterStatsPacket.SendUpdateStat(this, StatFlags.Luk);

            this.FlushDamageLog();
        }

        private void LoseEXP()
        {
            if (PrimaryStats.Job == 0 || PrimaryStats.Level >= 200) return;

            double lossPercent;
            if (Field.Town || Field.MobGen.Count == 0)
            {
                lossPercent = 0.01;
            }
            else
            {
                if (PrimaryStats.Job / 100 == 3)
                {
                    lossPercent = 0.08;
                }
                else
                {
                    lossPercent = 0.2;
                }
                lossPercent = lossPercent / PrimaryStats.Luk + 0.05;
            }
            var levelExp = Constants.GetLevelEXP(PrimaryStats.Level);

            var loseAmount = (levelExp * lossPercent);
            _characterLog.Info($"Player is losing {loseAmount} EXP ({lossPercent}) because of dying.");

            var rExp = (int)(PrimaryStats.EXP - loseAmount);
            SetEXP(rExp <= 0 ? 0 : rExp);
        }

        public void SetHide(bool Hidden, bool init)
        {
            var hideSkill = Constants.Gm.Skills.Hide;
            // Make sure that the user has the skill
            if (Skills.GetSkillLevel(hideSkill) == 0)
                Skills.AddSkillPoint(hideSkill);


            AdminPacket.Hide(this, Hidden);

            if (Hidden)
            {
                Buffs.AddBuff(hideSkill, 1);

                GMHideEnabled = true;
                if (!init) Field.RemovePlayer(this, true);
            }
            else
            {
                if (GMLevel == 1)
                {
                    MessagePacket.SendNotice(this, "GM interns cannot leave GM Hide.");
                    AdminPacket.Hide(this, true); //because client unhides you graphically when server rejects it
                }
                else
                {
                    GMHideEnabled = false;
                    if (!init)
                    {
                        PrimaryStats.RemoveByReference(hideSkill);
                        Field.ShowPlayer(this, true);
                    }
                }
            }
        }


        private void StartChangeMap(Map prevMap, Map newMap)
        {
            DestroyAdditionalProcess();

            prevMap.RemovePlayer(this);

            PortalCount++;
            Field = newMap;
        }

        private void FinishChangeMap(Map prevMap, Map newMap)
        {
            TryHideOnMapEnter();

            _characterLog.Info($"{Name} changed map to {newMap.ID}");

            MapPacket.SendChangeMap(this);
            newMap.AddPlayer(this);
            Summons.MigrateSummons(prevMap, newMap);

            Server.Instance.CenterConnection?.PlayerUpdateMap(this);
            Guild?.SendGuildInfoUpdate(this);
            PartyHPUpdate();
            RateCredits.SendUpdate();
        }

        // Change map, but take random spawn
        public void ChangeMap(int mapid)
        {
            ChangeMap(mapid, MapProvider.Maps[mapid].GetRandomStartPoint());
        }

        // Change map, but go to a specific portal
        public void ChangeMap(int mapid, string portalName)
        {
            if (portalName == null)
            {
                ChangeMap(mapid);
                return;
            }

            var newMap = MapProvider.Maps[mapid];
            if (newMap.Portals.TryGetValue(portalName, out var portal))
                ChangeMap(mapid, portal);
            else
                Program.MainForm.LogAppend("Did not find portal {0} for mapid {1}", portalName, mapid);
        }

        public void ChangeMap(int mapid, Portal to)
        {
            var prevMap = Field;
            var newMap = MapProvider.Maps[mapid];

            StartChangeMap(prevMap, newMap);
            {
                MapPosition = to.ID;

                Position = new Pos(to.X, (short) (to.Y - 40));
                MoveAction = 0;
                Foothold = 0;
                var pet = GetSpawnedPet();
                if (pet != null)
                {
                    var ml = pet.MovableLife;
                    ml.Position = new Pos(Position);
                    ml.MoveAction = 0;
                    ml.Foothold = 0;
                }
            }
            FinishChangeMap(prevMap, newMap);
        }

        public void ChangeMap(int mapid, byte partyMemberIdx, MysticDoor door)
        {
            var prevmap = Field;
            var newMap = MapProvider.Maps[mapid];

            StartChangeMap(prevmap, newMap);
            {
                // Something magical happens I suppose.
                // However, the server doesnt update the position
                // When a random portal is assigned, which would trigger a hack check.
                // So we are a bit clueless...
                MapPosition = (byte)(partyMemberIdx | (1 << 7));
                if (newMap.Town)
                {
                    Portal endingAt;
                    if (newMap.DoorPoints.Count > partyMemberIdx)
                    {
                        // Pick the one for the index
                        endingAt = newMap.DoorPoints[partyMemberIdx];
                    }
                    else
                    {
                        // Random.
                        endingAt = newMap.GetRandomStartPoint();
                    }
                    Position = new Pos(endingAt.X, endingAt.Y);
                }
                else
                {
                    Position = new Pos(door.X, door.Y);
                }

                MoveAction = 0;
                Foothold = 0;

                var pet = GetSpawnedPet();
                if (pet != null)
                {
                    var ml = pet.MovableLife;
                    ml.Position = new Pos(Position);
                    ml.MoveAction = 0;
                    ml.Foothold = 0;
                }
            }
            FinishChangeMap(prevmap, newMap);
        }

        public void SetHair(int id)
        {
            _characterLog.Info(new StatChangeLogRecord { value = id, type = "hair", add = false });
            Hair = id;
            CharacterStatsPacket.SendUpdateStat(this, StatFlags.Hair);
            MapPacket.SendAvatarModified(this, MapPacket.AvatarModFlag.Equips);//Because hair is a equip I guess
        }

        public void SetFace(int id)
        {
            _characterLog.Info(new StatChangeLogRecord { value = id, type = "face", add = false });
            Face = id;
            CharacterStatsPacket.SendUpdateStat(this, StatFlags.Eyes);
            MapPacket.SendAvatarModified(this, MapPacket.AvatarModFlag.Face);
        }

        public void SetSkin(byte id)
        {
            _characterLog.Info(new StatChangeLogRecord { value = id, type = "skin", add = false });
            Skin = id;
            CharacterStatsPacket.SendUpdateStat(this, StatFlags.Skin);
            MapPacket.SendAvatarModified(this, MapPacket.AvatarModFlag.Skin);
        }

        private byte ParseGenderString(string input)
        {
            switch (input.ToLower())
            {
                case "2":
                case "unisex":
                case "u": return 2;
                case "1":
                case "female":
                case "f": return 1;
                default: return 0;
            }
        }

        public void OnVarset(Character Sent, string Var, object Value, object Value2 = null, object Value3 = null)
        {
            if (this != Sent && Sent.IsGM && !Sent.IsAdmin) //Todo Admin levels
            {
                MessagePacket.SendNotice(Sent, "You don't have the permission to edit other players stats!");
                //$"{Sent.Name} tried to edit another players stats without premission"
            }
            else
            {
                try
                {
                    MapPacket.AvatarModFlag AvatarMod = 0;
                    StatFlags dwFlag = 0;

                    switch (Var.ToLower())
                    {
                        case "hp":
                            dwFlag |= StatFlags.Hp;
                            PrimaryStats.HP = Convert.ToInt16(Value);
                            break;
                        case "mp":
                            dwFlag |= StatFlags.Mp;
                            PrimaryStats.MP = Convert.ToInt16(Value);
                            break;
                        case "exp":
                            dwFlag |= StatFlags.Exp;
                            PrimaryStats.EXP = Convert.ToInt32(Value);
                            break;
                        case "maxhp":
                            dwFlag |= StatFlags.MaxHp;
                            if (Value.ToString() == "0")
                                Value = "1";
                            PrimaryStats.MaxHP = Convert.ToInt16(Value);
                            break;
                        case "maxmp":
                            dwFlag |= StatFlags.MaxMp;
                            if (Value.ToString() == "0")
                                Value = "1";
                            PrimaryStats.MaxMP = Convert.ToInt16(Value);
                            break;
                        case "ap":
                            dwFlag |= StatFlags.Ap;
                            PrimaryStats.AP = Convert.ToInt16(Value);
                            break;
                        case "sp":
                            dwFlag |= StatFlags.Sp;
                            PrimaryStats.SP = Convert.ToInt16(Value);
                            break;
                        case "str":
                            dwFlag |= StatFlags.Str;
                            PrimaryStats.Str = Convert.ToInt16(Value);
                            break;
                        case "dex":
                            dwFlag |= StatFlags.Dex;
                            PrimaryStats.Dex = Convert.ToInt16(Value);
                            break;
                        case "int":
                            dwFlag |= StatFlags.Int;
                            PrimaryStats.Int = Convert.ToInt16(Value);
                            break;
                        case "luk":
                            dwFlag |= StatFlags.Luk;
                            PrimaryStats.Luk = Convert.ToInt16(Value);
                            break;
                        case "fame":
                        case "pop":
                            dwFlag |= StatFlags.Fame;
                            PrimaryStats.Fame = Convert.ToInt16(Value);
                            break;
                        case "mesos":
                            dwFlag |= StatFlags.Mesos;
                            Inventory.Mesos = Convert.ToInt32(Value);
                            break;
                        case "job":
                            {
                                var Job = Convert.ToInt16(Value);
                                if (DataProvider.HasJob(Job) || Job == 0)
                                {
                                    dwFlag |= StatFlags.Job;
                                    PrimaryStats.Job = Job;
                                    Guild?.UpdatePlayer(this);
                                    MapPacket.SendJobChangeEffect(this);
                                }
                                else
                                    MessagePacket.SendNotice(Sent, $"Job {Job} does not exist.");
                                break;
                            }
                        case "skill":
                            {
                                var SkillID = Convert.ToInt32(Value);
                                if (DataProvider.Skills.TryGetValue(SkillID, out var Skill))
                                {
                                    if (Value2 == null)
                                        Value2 = Skill.MaxLevel;
                                    Skills.SetSkillPoint(SkillID, Convert.ToByte(Value2), true);
                                }
                                else
                                    MessagePacket.SendNotice(Sent, $"Skill {SkillID} does not exist.");
                                break;
                            }
                        case "level":
                            dwFlag |= StatFlags.Level;
                            Level = Convert.ToByte(Value);
                            Guild?.UpdatePlayer(this);
                            MapPacket.SendPlayerLevelupAnim(this);
                            break;
                        case "skin":
                            {
                                var SkinID = Convert.ToByte(Value);
                                if (SkinID >= 0 && SkinID < 6)
                                {
                                    AvatarMod |= MapPacket.AvatarModFlag.Skin;
                                    dwFlag |= StatFlags.Skin;
                                    Skin = SkinID;
                                }
                                else
                                    MessagePacket.SendNotice(Sent, $"Skin {SkinID} does not exist.");
                                break;
                            }
                        case "face":
                            {
                                var FaceID = Convert.ToInt32(Value);
                                if (DataProvider.Equips.ContainsKey(FaceID))
                                {
                                    AvatarMod |= MapPacket.AvatarModFlag.Face;
                                    dwFlag |= StatFlags.Eyes;
                                    Face = FaceID;
                                }
                                else
                                    MessagePacket.SendNotice(Sent, $"Face {FaceID} does not exist.");
                                break;
                            }
                        case "hair":
                            {
                                var HairID = Convert.ToInt32(Value);
                                if (DataProvider.Equips.ContainsKey(HairID))
                                {
                                    AvatarMod |= MapPacket.AvatarModFlag.Equips;
                                    dwFlag |= StatFlags.Hair;
                                    Hair = HairID;
                                }
                                else
                                    MessagePacket.SendNotice(Sent, $"Hair {HairID} does not exist.");
                                break;
                            }
                        case "gender":
                            {
                                Gender = ParseGenderString(Value.ToString());
                                Server.Instance.CharacterDatabase.RunQuery(
                                    "UPDATE characters SET gender = @gender WHERE id = @id",
                                    "@gender", Gender,
                                    "@id", ID
                                );

                                MessagePacket.SendNotice(this, $"Gender set to {(Gender == 0 ? "male" : (Gender == 2 ? "Unisex" : "female"))}. Please relog.");
                                break;
                            }
                        case "accgender":
                            {
                                var gender = ParseGenderString(Value.ToString());
                                Server.Instance.CharacterDatabase.RunQuery(
                                    "UPDATE users SET gender = @gender WHERE ID = @id",
                                    "@gender", gender,
                                    "@id", UserID
                                );

                                MessagePacket.SendNotice(this, $"Account gender set to {(gender == 0 ? "male" : (gender == 2 ? "Unisex" : "female"))}");
                                break;
                            }
                        case "map":
                        case "field":
                            {
                                var FieldID = Convert.ToInt32(Value);
                                if (MapProvider.Maps.ContainsKey(FieldID))
                                    ChangeMap(FieldID);
                                else
                                    MessagePacket.SendText(MessagePacket.MessageTypes.RedText, "Map not found.", this, MessagePacket.MessageMode.ToPlayer);
                                break;
                            }
                        default:
                            MessagePacket.SendNotice(Sent, $"{Var} is not a valid Variable!");
                            return;
                    }

                    if (dwFlag != 0)
                        CharacterStatsPacket.SendUpdateStat(this, dwFlag);

                    if (AvatarMod != 0)
                        MapPacket.SendAvatarModified(this, AvatarMod);
                }
                catch (Exception ex)
                {
                    MessagePacket.SendNotice(Sent, ex.Message);
                }
            }
        }

        public void OnPetVarset(string Var, string Value, bool Me)
        {
            MessagePacket.SendNotice(this, "Did you hear a cat just now? That damn thing haunts me.");
        }
    }
}