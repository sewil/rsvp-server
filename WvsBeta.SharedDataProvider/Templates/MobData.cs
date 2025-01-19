using System;
using System.Collections.Generic;

namespace WvsBeta.SharedDataProvider.Templates
{
    public enum MoveAbility
    {
        Stop = 0,
        Walk = 1,
        Jump = 2,
        Fly = 3,
    }


    public class MobData
    {
        public string Name { get; set; }
        public int ID { get; set; }
        public byte Level { get; set; }
        public bool Boss { get; set; }
        public bool Undead { get; set; }
        public bool BodyAttack { get; set; }
        public int EXP { get; set; }
        public int MaxHP { get; set; }
        public int MaxMP { get; set; }
        public int HPRecoverAmount { get; set; }
        public int MPRecoverAmount { get; set; }
        public uint HPTagColor { get; set; }
        public uint HPTagBgColor { get; set; }
        public int FixedDamage { get; set; }
        public short Speed { get; set; }
        public byte SummonType { get; set; }
        public bool Flies { get; set; }
        public bool NoGlobalReward { get; set; }
        public bool PublicReward { get; set; }
        public bool ExplosiveReward { get; set; }
        public List<int> Revive { get; set; }
        public Dictionary<byte, MobAttackData> Attacks { get; set; }
        public List<MobSkillData> Skills { get; set; }
        public float FS { get; set; }
        public int Eva { get; set; }
        public int Acc { get; set; }
        public int PAD { get; set; }
        public int PDD { get; set; }
        public int MAD { get; set; }
        public int MDD { get; set; }

        public MoveAbility MoveAbility { get; set; }
        public int RemoveAfterSeconds { get; set; }

        private string _elemAttr;
        public string elemAttr
        {
            get => _elemAttr;
            set
            {
                _elemAttr = value;
                SkillElement GetElemByName(char name)
                {
                    switch (char.ToUpper(name))
                    {
                        case 'P':
                            return SkillElement.Normal; // Physical
                        case 'I':
                            return SkillElement.Ice;
                        case 'F':
                            return SkillElement.Fire;
                        case 'L':
                            return SkillElement.Lightning;
                        case 'S':
                            return SkillElement.Poison;
                        case 'H':
                            return SkillElement.Holy;
                        case 'D':
                            return SkillElement.Dark;
                        case 'U':
                            return SkillElement.Undead;
                        default:

                            return SkillElement.Normal;
                    }
                }
                try
                {
                    for (int i = 0; i < _elemAttr.Length;)
                    {
                        var elem = GetElemByName(_elemAttr[i]);
                        i++;

                        // So it can have multiple digits, just support that
                        var numberPart = "";
                        for (; i < _elemAttr.Length; i++)
                        {
                            var c = _elemAttr[i];
                            if (!char.IsDigit(c)) break;
                            numberPart += c;
                        }


                        elemModifiers.Add(elem, int.Parse(numberPart));
                    }
                }
                catch (Exception ex)
                {
                    // ¯\_(ツ)_/¯
                }
            }
        }
        public Dictionary<SkillElement, int> elemModifiers { get; private set; } = new Dictionary<SkillElement, int>();
        public bool Pushed { get; set; }
        public bool NoRegen { get; set; }
        public bool Invincible { get; set; }
        public bool FirstAttack { get; set; }
        public int SelfDestructionHP { get; set; }
        public int EliminationPoints { get; set; }

        public override string ToString()
        {
            return $"{ID} ({Name})";
        }
    }
}