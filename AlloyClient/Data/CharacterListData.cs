using System;
using System.Linq;
using System.Xml.Linq;
using Alloy.Common;

namespace AlloyClient.Data;

public sealed class CharacterListData(XElement xml) : IGlobalData {
    public readonly int NextCharId = xml.GetAttribute("nextCharId", 0);
    public readonly int MaxNumChars = xml.GetAttribute("maxNumChars", 0);
    public readonly int CharSlotCost = xml.GetAttribute("charSlotCost", 2000);
    public readonly int[] OwnedSkins = xml.GetValue("OwnedSkins", "").FromCommaSepString();
    public readonly Character[] Characters = xml.Elements("Char").Select(c => new Character(c))
        .OrderByDescending(c => c.Experience).ToArray();
    public int AvailableSlots => Math.Max(0, MaxNumChars - Characters.Length);
}

public sealed class Character(XElement xml) {
    public readonly int Id = xml.GetAttribute("id", 0);
    public readonly ushort ObjectType = xml.GetValue<ushort>("ObjectType");
    public readonly int Level = xml.GetValue("Level", 0);
    public readonly int Experience = xml.GetValue("Exp", 0);
    public readonly ushort Skin = xml.GetValue<ushort>("Texture", 0);
    public readonly int Texture1 = xml.GetValue("Tex1", 0);
    public readonly int Texture2 = xml.GetValue("Tex2", 0);
    public readonly int[] Equipment = xml.GetValue("Equipment", "").FromCommaSepString();
    public readonly int CurrentFame = xml.GetValue("CurrentFame", 0);
    public readonly int MaxHitPoints = xml.GetValue("MaxHitPoints", 0);
    public readonly int HitPoints = xml.GetValue("HitPoints", 0);
    public readonly int MaxMagicPoints = xml.GetValue("MaxMagicPoints", 0);
    public readonly int MagicPoints = xml.GetValue("MagicPoints", 0);
    public readonly int Attack = xml.GetValue("Attack", 0);
    public readonly int Defense = xml.GetValue("Defense", 0);
    public readonly int Dexterity = xml.GetValue("Dexterity", 0);
    public readonly int Speed = xml.GetValue("Speed", 0);
    public readonly int Vitality = xml.GetValue("HpRegen", 0);
    public readonly int Wisdom = xml.GetValue("MpRegen", 0);
}
