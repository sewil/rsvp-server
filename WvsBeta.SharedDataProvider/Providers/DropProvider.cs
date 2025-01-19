using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WvsBeta.Common;
using WvsBeta.Game;
using WvsBeta.SharedDataProvider.Templates;
using WzTools.FileSystem;
using WzTools.Objects;

namespace WvsBeta.SharedDataProvider.Providers
{
    public class DropProvider : TemplateProvider<DropData[]>
    {
        public DropProvider(WzFileSystem fileSystem) : base(fileSystem)
        {
        }

        public override IDictionary<int, DropData[]> LoadAll()
        {
            var rewardProperty = FileSystem.GetProperty("Etc/Reward.img");

            return IterateAllToDict(rewardProperty.PropertyChildren.Where(property => property.Name != "global" && property.Name.StartsWith("m")), property =>
            {
                var drops = property.PropertyChildren.Select(DropDataFromProperty).ToArray();

                return ((int)Utils.ConvertNameToID(property.Name.Substring(1)), drops);
            }, x => x.Item1, x => x.Item2);
        }

        public DropData[] LoadGlobalDrops()
        {
            return FileSystem.GetProperty("Etc/Reward.img/global").PropertyChildren.Select(DropDataFromProperty).ToArray();
        }
        public IDictionary<string, DropData[]> LoadReactorDrops()
        {
            var reactorRewardProperty = FileSystem.GetProperty("Server/ReactorReward.img");

            return IterateAllToDict(reactorRewardProperty.PropertyChildren, property =>
            {
                var drops = property.PropertyChildren.Select(DropDataFromProperty).ToArray();

                return (property.Name, drops);
            }, x => x.Item1, x => x.Item2);
        }

        private DropData DropDataFromProperty(WzProperty dropProperty)
        {
            var dropData = new DropData
            {
                DateExpire = dropProperty.GetYYYYMMDDHHDateTime("dateExpire", DateTime.MaxValue),
                Period = dropProperty.GetUInt16("period") ?? 0,
                Mesos = dropProperty.GetInt32("money") ?? 0,
                ItemID = dropProperty.GetInt32("item") ?? 0,
                Min = dropProperty.GetInt16("min") ?? 0,
                Max = dropProperty.GetInt16("max") ?? 0,
                Premium = dropProperty.GetBool("premium") ?? false,

                ItemVariation = (ItemVariation?)dropProperty.GetInt32("var") ?? ItemVariation.Normal,
                
                MobMinLevel = dropProperty.GetInt32("mobminlvl") ?? 0,
                MobMaxLevel = dropProperty.GetInt32("mobmaxlvl") ?? int.MaxValue,
            };
            dropData.LoadLimitedDataFromProp(dropProperty);

            if (dropProperty.HasChild("prob"))
            {
                var probability = dropProperty.GetString("prob");
                var doubleProb = double.Parse(probability.Substring(probability.IndexOf(']') + 1), CultureInfo.InvariantCulture);
                dropData.Chance = CalculateDropChance(doubleProb);
            }
            else
            {
                throw new TemplateException(GetType(), $"No probability set on drop {dropData.ItemID} on {dropProperty.Parent.Name}");
            }

            return dropData;
        }

        public static int CalculateDropChance(double x)
        {
            if (x > 1.0 || x < 0.0)
                throw new Exception("Invalid dropchance");


            x *= DropData.DropChanceCalcFloat;
            var y = Math.Min((int)x, DropData.DropChanceCalcInt);

            return y;
        }
    }
}