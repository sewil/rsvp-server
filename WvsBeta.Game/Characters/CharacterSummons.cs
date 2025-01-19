using WvsBeta.Common;
using System.Collections.Generic;
using WvsBeta.Game.GameObjects;
using System.Linq;
using WvsBeta.Common.Sessions;

namespace WvsBeta.Game
{
    public class CharacterSummons
    {
        public readonly Character Chr;
        private readonly List<Summon> Summons = new List<Summon>();

        public CharacterSummons(Character c)
        {
            Chr = c;
        }

        public void RemoveSummon(int skillid)
        {
            if (!GetSummon(skillid, out var summon)) return;

            Chr.Field.Summons.DeregisterSummon(summon, 1);
            Summons.Remove(summon);
        }

        public void SetSummon(Summon sum)
        {
            RemoveSummon(sum.SkillId);
            Summons.Add(sum);
            Chr.Field.Summons.RegisterSummon(sum);
        }

        public bool GetSummon(int skillid, out Summon summon)
        {
            summon = Summons.FirstOrDefault(x => x.SkillId == skillid);
            return summon != null;
        }

        public void RemovePuppet()
        {
            RemoveSummon(Constants.Ranger.Skills.Puppet);
            RemoveSummon(Constants.Sniper.Skills.Puppet);
        }

        public void MigrateSummons(Map oldField, Map newField)
        {
            foreach (var summon in Summons.ToArray())
            {
                oldField.Summons.DeregisterSummon(summon, 0);

                if (summon is Puppet)
                {
                    RemoveSummon(summon.SkillId);
                }
                else
                {
                    // Move summon to user location
                    summon.Position = new Pos(Chr.Position);
                    newField.Summons.RegisterSummon(summon);
                }
            }
        }

        public void RemoveAllSummons()
        {
            Summons
                .ToArray()
                .ForEach(x => RemoveSummon(x.SkillId));
        }

        public void Update(long tCur)
        {
            Summons
                .Where(summon => tCur > summon.ExpireTime)
                // Make a copy, so it doesn't cause a Concurrent Modification Exception
                .ToArray()
                .ForEach(x => RemoveSummon(x.SkillId));
        }

        public void EncodeForCC(Packet pw)
        {
            //puppet doesnt transfer channels. Also, doesn't require summoning rock, so just recast it
            var summonsList = Summons.Where(s => !(s is Puppet)).ToArray();

            pw.WriteInt(summonsList.Length);

            foreach (var summon in summonsList)
            {
                pw.WriteInt(summon.SkillId);
                pw.WriteByte(summon.SkillLevel);
                pw.WriteByte(summon.MoveActionType);
                pw.WriteUShort(summon.FootholdSN);
                pw.WriteLong(summon.ExpireTime);
                pw.WriteShort(summon.Position.X);
                pw.WriteShort(summon.Position.Y);
            }
        }

        public void DecodeForCC(Packet pw)
        {
            var numSummons = pw.ReadInt();

            for (var i = 0; i < numSummons; i++)
            {
                var skillId = pw.ReadInt();
                var skillLevel = pw.ReadByte();
                var moveAction = pw.ReadByte<MoveActionType>();
                var footholdSN = pw.ReadUShort();
                var expireTime = pw.ReadLong();
                var x = pw.ReadShort();
                var y = pw.ReadShort();

                var summon = new Summon(Chr, skillId, skillLevel, x, y, moveAction, footholdSN, expireTime);
                SetSummon(summon);
            }
        }
    }
}
