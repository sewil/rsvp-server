using System;
using System.Collections.Generic;
using System.Text;

namespace WvsBeta.Game.Packets
{
    public enum UserEffect
    {
        LevelUp = 0,
        SkillAffected = 1,
        SkillAffected_Select = 2,
        Quest = 3,
        Pet = 4,
        SkillSpecial = 5, // Eg mesoguard effect. [int, skillid]
        ProtectOnDieItemUse = 6, // UNUSED: We have a custom implementation

        // 'Custom'

        // Show Monster Book card get effect and sound.
        MonsterBookCardGet = 15,
        // Scroll Enchant, show if scroll succeeded or not
        ItemMaker = 16,
        // Play Job Changed sound effect and animation
        JobChanged = 17,
        // Play quest complete sound effect
        QuestComplete = 18,
        // Play portal sound effect
        PlayPortalSE = 19,
    }
}
