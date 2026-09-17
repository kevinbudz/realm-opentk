using System;
using System.Linq;
using System.Xml.Linq;
using Alloy.Common;
using AlloyClient.Game;

namespace AlloyClient.Assets.XmlStructs;

public class ItemDesc {
    public readonly ushort ObjectType;
    public readonly string ObjectId;
    public readonly string Class;
    public readonly string DisplayId;
    public readonly string DisplayName;
    public readonly int Texture1;
    public readonly int Texture2;
    public readonly int SlotType;
    public readonly string Description;
    public readonly bool Consumable;
    public readonly bool InvUse;
    public readonly bool TypeOfConsumable;
    public readonly bool Soulbound;
    public readonly bool Potion;
    public readonly bool Usable;
    public readonly bool Resurrects;
    public readonly float RateOfFire;
    public readonly int Tier;
    public readonly int BagType;
    public readonly int FameBonus;
    public readonly int NumProjectiles;
    public readonly float ArcGap;
    public readonly int MpCost;
    public readonly float Cooldown;
    public readonly int Doses;
    public readonly string SuccessorId;
    public readonly bool Backpack;
    public readonly bool LDBoosted;
    public readonly bool LTBoosted;
    public readonly bool XpBoost;
    public readonly float Timer;
    public readonly int MpEndCost;

    public readonly StatBoostDesc[] StatBoosts;
    public readonly ActivateEffectDesc[] ActivateEffects;
    public readonly ProjectileDesc Projectiles;

    // Tooltip parity fields (mirror the Flash hasOwnProperty checks).
    public readonly bool HasTier;
    public readonly bool IsSet;
    public readonly string SetName;
    public readonly bool Treasure;
    public readonly bool PetFood;
    public readonly bool NoTierTag;
    public readonly bool IsPermaPet;
    public readonly int ScaleValue;
    public readonly bool HasSlotType;
    public readonly bool HasNumProjectiles;
    public readonly bool HasMpCost;
    public readonly bool HasCooldown;
    public readonly bool HasFameBonus;
    public readonly bool HasDoses;
    public readonly ExtraTooltipInfo[] ExtraTooltipData;
    public readonly EquipRequirementDesc[] EquipRequirements;

    public ItemDesc(ushort type, XElement xml) {
        ObjectType = type;
        ObjectId = xml.GetAttribute<string>("id");
        Class = xml.GetValue<string>("Class");
        DisplayId = xml.GetValue<string>("DisplayId");
        DisplayName = string.IsNullOrWhiteSpace(DisplayId) ? ObjectId : DisplayId;
        Texture1 = xml.GetValue<int>("Tex1");
        Texture2 = xml.GetValue<int>("Tex2");
        SlotType = xml.GetValue<int>("SlotType");
        Description = xml.GetValue<string>("Description");
        Consumable = xml.HasElement("Consumable");
        Soulbound = xml.HasElement("Soulbound");
        Potion = xml.HasElement("Potion");
        Usable = xml.HasElement("Usable");
        Resurrects = xml.HasElement("Resurrects");
        RateOfFire = xml.GetValue<float>("RateOfFire", 1f);
        if (xml.HasElement("Tier"))
            Tier = xml.GetValue<int>("Tier");
        BagType = xml.GetValue<int>("BagType");
        FameBonus = xml.GetValue<int>("FameBonus");
        NumProjectiles = xml.GetValue<int>("NumProjectiles", 1);
        ArcGap = xml.GetValue<float>("ArcGap", 11.25f);
        MpCost = xml.GetValue<int>("MpCost");
        Cooldown = xml.GetValue<float>("Cooldown", 0.5f);
        Doses = xml.GetValue<int>("Doses");
        SuccessorId = xml.GetValue<string>("SuccessorId");
        Backpack = xml.HasElement("Backpack");
        LDBoosted = xml.HasElement("LDBoosted");
        LTBoosted = xml.HasElement("LTBoosted");
        XpBoost = xml.HasElement("XpBoost");
        Timer = xml.GetValue<float>("Timer");
        MpEndCost = xml.GetValue<int>("MpEndCost", 0);
        InvUse = xml.HasElement("InvUse");
        TypeOfConsumable = InvUse || Consumable;

        StatBoosts = xml.Elements("ActivateOnEquip").Select(i => new StatBoostDesc(i)).ToArray();
        ActivateEffects = xml.Elements("Activate").Select(i => new ActivateEffectDesc(i)).ToArray();
        Projectiles = xml.HasElement("Projectile") ? new ProjectileDesc(xml.Element("Projectile")) : null;

        HasTier = xml.HasElement("Tier");
        IsSet = xml.HasAttribute("setType");
        SetName = xml.GetAttribute<string>("setName");
        Treasure = xml.HasElement("Treasure");
        PetFood = xml.HasElement("PetFood");
        NoTierTag = xml.HasElement("NoTierTag");
        IsPermaPet = xml.Elements("Activate").Any(e => e.Value == "PermaPet");
        ScaleValue = xml.GetValue<int>("ScaleValue", 5);
        HasSlotType = xml.HasElement("SlotType");
        HasNumProjectiles = xml.HasElement("NumProjectiles");
        HasMpCost = xml.HasElement("MpCost");
        HasCooldown = xml.HasElement("Cooldown");
        HasFameBonus = xml.HasElement("FameBonus");
        HasDoses = xml.HasElement("Doses");
        ExtraTooltipData = xml.Element("ExtraTooltipData")?.Elements("EffectInfo")
            .Select(e => new ExtraTooltipInfo(e.GetAttribute<string>("name"), e.GetAttribute<string>("description")))
            .ToArray() ?? [];
        EquipRequirements = xml.Elements("EquipRequirement")
            .Select(e => new EquipRequirementDesc(e.Value, e.GetAttribute<int>("stat"), e.GetAttribute<int>("value")))
            .ToArray();
    }
}

public class ExtraTooltipInfo(string name, string description) {
    public string Name = name;
    public string Description = description;
}

public class EquipRequirementDesc(string kind, int stat, int value) {
    public string Kind = kind;
    public int Stat = stat;
    public int Value = value;
}

public class StatBoostDesc(XElement xml) {
    // XML carries these as attributes (<ActivateOnEquip stat="21" amount="2">).
    public int Stat = xml.GetAttribute<int>("stat");
    public int Amount = xml.GetAttribute<int>("amount");
}

public class ProjectileDesc {

    public XElement Root { get; }
    
    public readonly int BulletType;
    public readonly string ObjectId;
    public readonly float Speed;
    public readonly int MinDamage;
    public readonly int MaxDamage;
    public readonly int LifetimeMS;
    public readonly ParticleTrailDesc Trail;
    public readonly bool MultiHit;
    public readonly bool PassesCover;
    public readonly bool Parametric;
    public readonly bool Boomerang;
    public readonly bool ArmorPiercing;
    public readonly bool Wavy;

    public readonly ConditionEffectDesc[] Effects;

    public readonly float Amplitude;
    public readonly float Frequency;
    public readonly float Magnitude;

    public ProjectileDesc(XElement xml) {
        Root = xml;
        BulletType = xml.GetAttribute<int>("id");
        ObjectId = xml.GetValue<string>("ObjectId");
        LifetimeMS = (int)xml.GetValue<float>("LifetimeMS");
        Speed = xml.GetValue<float>("Speed", 100);

        var dmg = xml.Element("Damage");
        if (dmg != null)
            MinDamage = MaxDamage = xml.GetValue<int>("Damage");
        else {
            MinDamage = xml.GetValue<int>("MinDamage");
            MaxDamage = xml.GetValue<int>("MaxDamage");
        }

        Effects = xml.Elements("ConditionEffect").Select(x => new ConditionEffectDesc(x.Value, x.GetAttribute<float>("duration"))).ToArray();
        
        Trail = xml.HasElement("ParticleTrail") ? new ParticleTrailDesc(xml.Element("ParticleTrail")) : null;

        MultiHit = xml.HasElement("MultiHit");
        PassesCover = xml.HasElement("PassesCover");
        ArmorPiercing = xml.HasElement("ArmorPiercing");
        Wavy = xml.HasElement("Wavy");
        Parametric = xml.HasElement("Parametric");
        Boomerang = xml.HasElement("Boomerang");

        Amplitude = xml.GetValue<float>("Amplitude", 0);
        Frequency = xml.GetValue<float>("Frequency", 1);
        Magnitude = xml.GetValue<float>("Magnitude", 3);
    }
}

public class ConditionEffectDesc {

    public int EffectId;
    public string EffectName;
    public int DurationMS;
    public float Range;
    
    public ConditionEffectDesc(string eff, float duration, float range = 0f) {
        EffectName = eff;
        EffectId = ConditionEffect.ValueFromName(eff);
        if (duration < 100) { // nah, this is crazy
            duration *= 1000;
        }
        DurationMS = (int)duration;
        Range = range;
    }
}

public class ParticleTrailDesc {

    public uint Color;
    public int LifetimeMS;
    public int Size;

    public ParticleTrailDesc(XElement xml) {
        Color = string.IsNullOrEmpty(xml.Value) ? 0 : Convert.ToUInt32(xml.Value, 16);
        LifetimeMS = xml.GetAttribute<int>("lifetimeMS");
        Size = xml.GetAttribute<int>("size");
    }
}

public class ActivateEffectDesc {
    public readonly string Effect;
    public readonly ConditionEffect ConditionEffect;
    public readonly ConditionEffect CheckExistingEffect;

    public readonly int TotalDamage;
    public readonly float Radius;
    public readonly float EffectDuration;
    public readonly float DurationSec;
    public readonly int DurationMS;
    public readonly int Amount;
    public readonly float Range;
    public readonly float MaximumDistance;
    public readonly string ObjectId;
    public readonly string Id;
    public readonly int MaxTargets;
    public readonly uint? Color;
    public readonly int Stats;
    public readonly float Cooldown;
    public readonly bool RemoveSelf;
    public readonly string DungeonName;
    public readonly string LockedName;

    public ActivateEffectDesc(XElement xml) {
        Effect = xml.Value;

        if (xml.HasAttribute("effect"))
            Enum.TryParse(xml.GetAttribute<string>("effect"), out ConditionEffect);

        if (xml.HasAttribute("condEffect"))
            Enum.TryParse(xml.GetAttribute<string>("condEffect"), out ConditionEffect);

        if (xml.HasAttribute("checkExistingEffect"))
            Enum.TryParse(xml.GetAttribute<string>("checkExistingEffect"), out CheckExistingEffect);

        if (xml.HasAttribute("color")) {
            Color = xml.GetAttribute<uint>("color");
        }

        TotalDamage = xml.GetAttribute<int>("totalDamage");
        Radius = xml.GetAttribute<float>("radius");
        EffectDuration = xml.GetAttribute<float>("condDuration");
        DurationSec = xml.GetAttribute<float>("duration");
        DurationMS = (int) (DurationSec * 1000.0f);
        Amount = xml.GetAttribute<int>("amount");
        Range = xml.GetAttribute<float>("range");
        ObjectId = xml.GetAttribute<string>("objectId");
        Id = xml.GetAttribute<string>("id");
        MaximumDistance = xml.GetAttribute<float>("maxDistance");
        MaxTargets = xml.GetAttribute<int>("maxTargets");
        Stats = xml.GetAttribute<int>("stat");
        Cooldown = xml.GetAttribute<float>("cooldown");
        RemoveSelf = xml.GetAttribute<bool>("removeSelf");
        DungeonName = xml.GetAttribute<string>("dungeonName");
        LockedName = xml.GetAttribute<string>("lockedName");
    }
}