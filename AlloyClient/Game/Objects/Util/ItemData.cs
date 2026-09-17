namespace AlloyClient.Game.Objects.Util;

// Port of com.company.assembleegameclient.util.ItemData.
// Decodes the per-item bonus bitmask (ITEMDATA_n stats) that the server
// attaches to individual item instances: low bits are the T0-T7 rank,
// high bits select which stats scale with that rank.
public static class ItemData {
    public const uint T0Bit = 1u << 0;
    public const uint T1Bit = 1u << 1;
    public const uint T2Bit = 1u << 2;
    public const uint T3Bit = 1u << 3;
    public const uint T4Bit = 1u << 4;
    public const uint T5Bit = 1u << 5;
    public const uint T6Bit = 1u << 6;
    public const uint T7Bit = 1u << 7;

    public const uint MaxHpBit = 1u << 8;
    public const uint MaxMpBit = 1u << 9;
    public const uint AttackBit = 1u << 10;
    public const uint DefenseBit = 1u << 11;
    public const uint SpeedBit = 1u << 12;
    public const uint DexterityBit = 1u << 13;
    public const uint VitalityBit = 1u << 14;
    public const uint WisdomBit = 1u << 15;
    public const uint RateOfFireBit = 1u << 16;
    public const uint DamageBit = 1u << 17;
    public const uint CooldownBit = 1u << 18;
    public const uint FameBonusBit = 1u << 19;

    public const double CooldownMultiplier = 0.05;
    public const double DamageMultiplier = 0.05;
    public const double RateOfFireMultiplier = 0.05;

    public static bool HasStat(int data, uint bit) {
        if (data == -1)
            return false;
        return ((uint)data & bit) != 0;
    }

    public static double GetStat(int data, uint bit, double multiplier) {
        var rank = GetRank(data);
        if (rank == -1)
            return 0;
        return (HasStat(data, bit) ? rank : 0) * multiplier;
    }

    public static int GetRank(int data) {
        if (data == -1)
            return -1;
        if (HasStat(data, T0Bit)) return 1;
        if (HasStat(data, T1Bit)) return 2;
        if (HasStat(data, T2Bit)) return 3;
        if (HasStat(data, T3Bit)) return 4;
        if (HasStat(data, T4Bit)) return 5;
        if (HasStat(data, T5Bit)) return 6;
        if (HasStat(data, T6Bit)) return 7;
        if (HasStat(data, T7Bit)) return 8;
        return -1;
    }

    // Title color for a bonused item, or -1 when the item has no rank color.
    public static int GetColor(int data) {
        if (HasStat(data, T0Bit)) return 0x00a6ff;
        if (HasStat(data, T1Bit)) return 0x7300ff;
        if (HasStat(data, T2Bit)) return 0xffc800;
        if (HasStat(data, T3Bit)) return 0x84ff00;
        if (HasStat(data, T4Bit)) return 0xf542e6;
        if (HasStat(data, T5Bit)) return 0x00ffdd;
        if (HasStat(data, T6Bit)) return 0xffffff;
        if (HasStat(data, T7Bit)) return 0xff5500;
        return -1;
    }

    public static string GetColorString(int data) {
        if (HasStat(data, T0Bit)) return "#00a6ff";
        if (HasStat(data, T1Bit)) return "#7300ff";
        if (HasStat(data, T2Bit)) return "#ffc800";
        if (HasStat(data, T3Bit)) return "#84ff00";
        if (HasStat(data, T4Bit)) return "#f542e6";
        if (HasStat(data, T5Bit)) return "#00ffdd";
        if (HasStat(data, T6Bit)) return "#ffffff";
        if (HasStat(data, T7Bit)) return "#ff5500";
        return "";
    }
}
