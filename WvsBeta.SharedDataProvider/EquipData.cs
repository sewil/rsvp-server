using System.Collections.Generic;
using WvsBeta.Game;

public class EquipData : EquipStanceInfo
{
    public string Name { get; set; }
    public int ID { get; set; }
    public bool Cash { get; set; }
    public byte Slots { get; set; }
    public byte RequiredLevel { get; set; }
    public ushort RequiredStrength { get; set; }
    public ushort RequiredDexterity { get; set; }
    public ushort RequiredIntellect { get; set; }
    public ushort RequiredLuck { get; set; }
    public short RequiredJob { get; set; }
    public int Price { get; set; }
    public ushort RequiredFame { get; set; }
    public short HP { get; set; }
    public short MP { get; set; }
    public short Strength { get; set; }
    public short Dexterity { get; set; }
    public short Intellect { get; set; }
    public short Luck { get; set; }
    public short Craft { get; set; }
    public short Hands => Craft;
    public short WeaponAttack { get; set; }
    public short MagicAttack { get; set; }
    public short WeaponDefense { get; set; }
    public short MagicDefense { get; set; }
    public short Accuracy { get; set; }
    public short Avoidance { get; set; }
    public short Speed { get; set; }
    public short Jump { get; set; }
    public short KnockbackRate { get; set; }
    public bool TimeLimited { get; set; }
    public byte Attack { get; set; }
    public float RecoveryRate { get; set; } = 1.0f;

    public byte AttackSpeed { get; set; }
    public bool Quest { get; set; }
    public bool Only { get; set; }

    public QuestLimited QuestLimited { get; set; }

    public List<int> Pets { get; set; }

    public ItemVariation ItemVariation { get; set; }
    public bool HideRewardInfo { get; set; }

    public Dictionary<byte, EquipStanceInfo> EquipStanceInfos { get; set; }


    public static int GetPointsForStat(int currentStat, int baseStat)
    {
        var maxDiff = EquipItem.GetMaxDistribution(baseStat, ItemVariation.Normal);
        var currentDiff = currentStat - baseStat;

        if (maxDiff == 0) return 0;

        // % of stats given by RNG
        return (currentDiff * 100) / maxDiff;
    }

    public int CalculateEquipQuality(EquipItem ei)
    {
        int sum = 0;

        // HP and MP gets added by a lot, so don't count them in as much
        sum += GetPointsForStat(ei.HP, HP) / 50;
        sum += GetPointsForStat(ei.MP, MP) / 50;
        sum += GetPointsForStat(ei.Str, Strength);
        sum += GetPointsForStat(ei.Dex, Dexterity);
        sum += GetPointsForStat(ei.Int, Intellect);
        sum += GetPointsForStat(ei.Luk, Luck);
        sum += GetPointsForStat(ei.Acc, Accuracy);
        sum += GetPointsForStat(ei.Avo, Avoidance);
        sum += GetPointsForStat(ei.Hands, Hands);
        sum += GetPointsForStat(ei.Watk, WeaponAttack);
        sum += GetPointsForStat(ei.Wdef, WeaponDefense);
        sum += GetPointsForStat(ei.Matk, MagicAttack);
        sum += GetPointsForStat(ei.Mdef, MagicDefense);
        sum += GetPointsForStat(ei.Speed, Speed);
        sum += GetPointsForStat(ei.Jump, Jump);

        var scrollsUsed = Slots - ei.Slots;
        if (scrollsUsed > 0)
        {
            var scrollsPassed = scrollsUsed - ei.Scrolls;

            // How many scrolls have passed
            sum += (scrollsPassed * 100) / scrollsUsed;
        }

        return sum;
    }

}


public class EquipStanceInfo
{
    public string[] AnimationFrames { get; set; }
}