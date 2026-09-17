namespace AlloyClient.Game.Objects.Util;

public static class StatsUtil {

    public static string FromId(int statId) {
        return statId switch {
            0 => "Maximum HP",
            3 => "Maximum MP",
            20 => "Attack",
            21 => "Defense",
            22 => "Speed",
            28 => "Dexterity",
            26 => "Vitality",
            27 => "Wisdom",
            _ => "Invalid Stat!"
        };
    }

    // Translates a network stat id (as used by ActivateOnEquip/Activate
    // 'stat' attributes in the game data) to an internal boost slot
    // 0-7 (MaxHP, MaxMP, Attack, Defense, Speed, Dexterity, Vitality,
    // Wisdom), mirroring StatData.statToBoostIndex. Returns -1 for
    // non-boost stats.
    public static int StatToBoostIndex(int stat) {
        return stat switch {
            0 => 0,
            3 => 1,
            20 => 2,
            21 => 3,
            22 => 4,
            28 => 5,
            26 => 6,
            27 => 7,
            _ => -1
        };
    }

    // Boost-slot display name, mirroring StatData.statToName.
    public static string BoostSlotToName(int slot) {
        return slot switch {
            0 => "Maximum HP",
            1 => "Maximum MP",
            2 => "Attack",
            3 => "Defense",
            4 => "Speed",
            5 => "Dexterity",
            6 => "Vitality",
            7 => "Wisdom",
            _ => "Unknown Stat"
        };
    }

    // Requirement display name. Flash passes the raw EquipRequirement stat
    // to statToName (boost slots 0-7), which prints "Unknown Stat" for the
    // protocol ids playerMeetsRequirement actually checks (20-25, 7), so
    // resolve those too instead of reproducing the blank label.
    public static string RequirementStatToName(int stat) {
        if (stat >= 0 && stat <= 7)
            return BoostSlotToName(stat);
        return stat switch {
            20 => "Attack",
            21 => "Defense",
            22 => "Speed",
            23 => "Vitality",
            24 => "Wisdom",
            25 => "Dexterity",
            26 => "Vitality",
            27 => "Wisdom",
            28 => "Dexterity",
            3 => "Maximum MP",
            7 => "Level",
            _ => "Unknown Stat"
        };
    }

    public static bool MeetsRequirement(int stat, int value, int maxHp, int maxMp, int level,
        int attack, int defense, int speed, int vitality, int wisdom, int dexterity) {
        return stat switch {
            0 => maxHp >= value,
            3 => maxMp >= value,
            7 => level >= value,
            20 => attack >= value,
            21 => defense >= value,
            22 => speed >= value,
            23 => vitality >= value,
            24 => wisdom >= value,
            25 => dexterity >= value,
            _ => false
        };
    }
}