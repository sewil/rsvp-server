using WvsBeta.Common;
using WzTools.FileSystem;

namespace WvsBeta.DataValidator
{
    static class RewardsValidator
    {
        public static void Validate(WzFileSystem fileSystem)
        {
            ValidateMobRewards(fileSystem);
            ValidateReactorRewards(fileSystem);
        }

        private static void ValidateMobRewards(WzFileSystem fileSystem)
        {
            Console.WriteLine("Validating mob rewards...");

            var props = fileSystem.GetProperty("Etc/Reward.img");
            foreach (var prop in props.PropertyChildren)
            {
                string objID = prop.Name;
                foreach (var reward in prop.PropertyChildren)
                {
                    var itemID = reward.GetInt32("item");
                    if (!itemID.HasValue) continue;
                    ValidateReward(fileSystem, objID, itemID.Value);
                }
            }
        }

        private static void ValidateReactorRewards(WzFileSystem fileSystem)
        {
            Console.WriteLine("Validating reactor rewards...");

            var props = fileSystem.GetProperty("Server/ReactorReward.img");

            foreach (var prop in props.PropertyChildren)
            {
                string objID = prop.Name;
                var itemID = prop.GetProperty(0).GetInt32("item");
                if (!itemID.HasValue) continue;
                ValidateReward(fileSystem, objID, itemID.Value);
            }
        }

        private static void ValidateReward(WzFileSystem fileSystem, string objID, int itemID)
        {
            string path = "";
            switch (Constants.getInventory(itemID))
            {
                case 1:
                    switch (Constants.getItemType(itemID))
                    {
                        case Constants.Items.Types.ItemTypes.ArmorHelm:
                            path = "Character/Cap";
                            break;
                        case Constants.Items.Types.ItemTypes.AccessoryFace:
                        case Constants.Items.Types.ItemTypes.AccessoryEye:
                        case Constants.Items.Types.ItemTypes.AccessoryEarring:
                            path = "Character/Accessory";
                            break;
                        case Constants.Items.Types.ItemTypes.ArmorTop:
                            path = "Character/Coat";
                            break;
                        case Constants.Items.Types.ItemTypes.ArmorOverall:
                            path = "Character/Longcoat";
                            break;
                        case Constants.Items.Types.ItemTypes.ArmorBottom:
                            path = "Character/Pants";
                            break;
                        case Constants.Items.Types.ItemTypes.ArmorShoe:
                            path = "Character/Shoes";
                            break;
                        case Constants.Items.Types.ItemTypes.ArmorGlove:
                            path = "Character/Glove";
                            break;
                        case Constants.Items.Types.ItemTypes.ArmorShield:
                            path = "Character/Shield";
                            break;
                        case Constants.Items.Types.ItemTypes.ArmorCape:
                            path = "Character/Cape";
                            break;
                        case Constants.Items.Types.ItemTypes.ArmorRing:
                            path = "Character/Ring";
                            break;
                        case Constants.Items.Types.ItemTypes.Weapon1hSword:
                        case Constants.Items.Types.ItemTypes.Weapon1hAxe:
                        case Constants.Items.Types.ItemTypes.Weapon1hMace:
                        case Constants.Items.Types.ItemTypes.WeaponDagger:
                        case Constants.Items.Types.ItemTypes.WeaponWand:
                        case Constants.Items.Types.ItemTypes.WeaponStaff:
                        case Constants.Items.Types.ItemTypes.Weapon2hSword:
                        case Constants.Items.Types.ItemTypes.Weapon2hAxe:
                        case Constants.Items.Types.ItemTypes.Weapon2hMace:
                        case Constants.Items.Types.ItemTypes.WeaponSpear:
                        case Constants.Items.Types.ItemTypes.WeaponPolearm:
                        case Constants.Items.Types.ItemTypes.WeaponBow:
                        case Constants.Items.Types.ItemTypes.WeaponCrossbow:
                        case Constants.Items.Types.ItemTypes.WeaponClaw:
                        case Constants.Items.Types.ItemTypes.WeaponSkillFX:
                        case Constants.Items.Types.ItemTypes.WeaponCash:
                            path = "Character/Weapon";
                            break;
                        case Constants.Items.Types.ItemTypes.PetEquip:
                            path = "Character/PetEquip";
                            break;
                        default:
                            break;
                    }
                    path += $"/{itemID.ToString().PadLeft(8, '0')}.img";
                    break;
                case 2:
                    path = $"Item/Consume/{itemID.ToString().Substring(0, 3).PadLeft(4, '0')}.img/{itemID.ToString().PadLeft(8, '0')}";
                    break;
                case 3:
                    path = $"Item/Install/{itemID.ToString().Substring(0, 3).PadLeft(4, '0')}.img/{itemID.ToString().PadLeft(8, '0')}";
                    break;
                case 4:
                    path = $"Item/Etc/{itemID.ToString().Substring(0, 3).PadLeft(4, '0')}.img/{itemID.ToString().PadLeft(8, '0')}";
                    break;
                case 5:
                    path = $"Item/Pet/{itemID}.img";
                    break;
                default:
                    break;
            }

            if (!fileSystem.PathExists(path))
            {
                Console.WriteLine(string.Format("Could not find item {0} for obj {1}, check {2}", itemID, objID, path));
            }
        }
    }
}
