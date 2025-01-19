using System;
using WvsBeta.Common;

namespace WvsBeta.Game
{
    static class Pet
    {
        public static void IncreaseCloseness(Character chr, PetItem petItem, short inc)
        {
            if (petItem.Closeness >= Constants.MaxCloseness) return;
            petItem.Closeness = (short)Math.Max(0, Math.Min(Constants.MaxCloseness, petItem.Closeness + inc));

            var possibleLevel = GetLevel(petItem);
            // We can only increase level, not decrease
            if (possibleLevel > petItem.Level)
            {
                petItem.Level = possibleLevel;
                PetsPacket.SendPetLevelup(chr);
            }


            InventoryPacket.UpdateItems(chr, petItem);
        }

        public static byte GetLevel(PetItem petItem)
        {
            var expCurve = Constants.PetExp;
            for (byte i = 0; i < expCurve.Length; i++)
            {
                if (expCurve[i] > petItem.Closeness)
                    return (byte)(i + 1);
            }
            return 1;
        }
        
        public static bool IsNamedPet(PetItem petItem) => petItem?.Template.Name != petItem?.Name;
    }
}
