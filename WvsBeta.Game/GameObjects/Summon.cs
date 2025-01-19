using WvsBeta.Common;

namespace WvsBeta.Game.GameObjects
{
    public class Summon : MovableLife
    {
        public readonly Character Owner;
        public int OwnerId => Owner.ID;
        public readonly int SkillId;
        public readonly byte SkillLevel;
        public readonly ushort FootholdSN;
        public readonly long ExpireTime;

        public Summon(Character owner, int skillId, byte skillLevel, short x, short y, MoveActionType moveAction, ushort footholdSN, long expireTime)
        {
            Owner = owner;
            SkillId = skillId;
            SkillLevel = skillLevel;
            Position.X = x;
            Position.Y = y;
            FootholdSN = footholdSN;
            ExpireTime = expireTime;
            SetMoveActionType(moveAction);
        }
    }

    public class Puppet : Summon
    {
        private int HP;

        public Puppet(Character owner, int skillId, byte skillLevel, short x, short y, MoveActionType moveAction, ushort footholdSN, long expireTime, int hp) : base(owner, skillId, skillLevel, x, y, moveAction, footholdSN, expireTime)
        {
            HP = hp;
        }

        public void TakeDamage(int amount)
        {
            HP -= amount;
            if (HP < 0)
            {
                Owner.Summons.RemoveSummon(SkillId);
            }
        }
    }
}
