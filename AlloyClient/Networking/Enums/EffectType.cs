namespace AlloyClient.Networking.Enums;

//Wire values must match realm-server RotMG.Common.ShowEffectIndex and
//realm-client ShowEffect.as. Do NOT reorder or renumber: every value is
//explicit and new ids are appended at the end, never inserted.
public enum EffectType {
    Unknown = 0,
    Heal = 1, // target, color
    Teleport = 2, // pos1
    Stream = 3, // pos1, pos2, color
    Throw = 4, // target, pos1, color (+ optional pos2.x = flight ms)
    Nova = 5, // target, pos1.x = radius, color
    Poison = 6, // target, color
    Line = 7, // target, pos1, color
    Burst = 8, // target, pos1, pos2, color
    Flow = 9, // target, pos1, color
    Ring = 10, // target, pos1.x = radius, color
    Lightning = 11, // target, pos1, color, pos2.x = particle size
    Collapse = 12, // target, pos1, pos2, color
    ConeBlast = 13, // target, pos1, pos2.x = radius, color
    Jitter = 14, // camera shake
    Flash = 15, // target, color, pos1
    ThrowProjectile = 16 // pos1, pos2, color
}
