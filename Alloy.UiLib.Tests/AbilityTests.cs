using System.Xml.Linq;
using Alloy.Engine;
using AlloyClient;
using AlloyClient.Assets.Libraries;
using AlloyClient.Assets.XmlStructs;
using AlloyClient.Game;
using AlloyClient.Game.Objects;
using AlloyClient.Game.Objects.Util;
using AlloyClient.Networking;
using AlloyClient.Networking.Packets.Outgoing;
using OpenTK.Mathematics;
using OpenTK.Platform;

namespace Alloy.UiLib.Tests;

// Regression for space doing nothing: the slot-1 ability must pass
// Usable/MP/cooldown gating exactly like Flash Player.useAltWeapon, with
// Space and its Settings.Special alias both routing through the same public
// input, and UseItem plus ability PlayerShoot observable across frames.
internal static class AbilityTests {
    private const string AbilityXml = """
        <Object type="2000" id="Test Spell">
          <Class>Equipment</Class>
          <Item/>
          <SlotType>18</SlotType>
          <Usable/>
          <MpCost>50</MpCost>
          <Cooldown>0.5</Cooldown>
          <Activate>HealNova</Activate>
        </Object>
        """;

    private const string NoCooldownXml = """
        <Object type="2001" id="Test Cloak">
          <Class>Equipment</Class>
          <Item/>
          <SlotType>19</SlotType>
          <Usable/>
          <MpCost>10</MpCost>
          <Activate>Decoy</Activate>
        </Object>
        """;

    private const string NonUsableXml = """
        <Object type="2002" id="Test Sword">
          <Class>Equipment</Class>
          <Item/>
          <SlotType>1</SlotType>
        </Object>
        """;

    private const string ShootXml = """
        <Object type="2003" id="Test Quiver">
          <Class>Equipment</Class>
          <Item/>
          <SlotType>20</SlotType>
          <Usable/>
          <MpCost>5</MpCost>
          <Cooldown>0.5</Cooldown>
          <Activate>Shoot</Activate>
        </Object>
        """;

    private const string ShootPropsXml = """
        <Object type="2003" id="Test Quiver">
          <Class>Equipment</Class>
          <Projectile id="0">
            <ObjectId>TestBullet</ObjectId>
            <LifetimeMS>1000</LifetimeMS>
            <Speed>100</Speed>
            <Damage>10</Damage>
          </Projectile>
          <NumProjectiles>3</NumProjectiles>
          <ArcGap>11.25</ArcGap>
        </Object>
        """;

    private const string BulletPropsXml = """
        <Object type="2004" id="TestBullet">
          <Class>Projectile</Class>
        </Object>
        """;

    public static void Run() {
        RejectsMissingOrNonUsable();
        RejectsLowMp();
        EnforcesCooldown();
        CooldownDefaultsToFlash200ms();
        DetectsShootAbility();
        SpaceAndAliasRouteThroughSpecial();
        UsableAbilitySendsUseItemAcrossFrames();
        LowMpSendsNothingThroughPublicInput();
        ShootAbilitySendsAbilityShoot();
        UseItemTimeSharesMoveEpoch();
    }

    private static void RejectsMissingOrNonUsable() {
        Equal(false, AbilityHelper.CanUse(null, 100, 1000, 0));
        var empty = new ItemDesc(0, XElement.Parse(
            """<Object type="0" id="E"><Class>Equipment</Class><Item/><SlotType>18</SlotType></Object>"""));
        Equal(false, AbilityHelper.CanUse(empty, 100, 1000, 0));
        var sword = new ItemDesc(2002, XElement.Parse(NonUsableXml));
        Equal(false, AbilityHelper.CanUse(sword, 100, 1000, 0));
    }

    private static void RejectsLowMp() {
        var spell = new ItemDesc(2000, XElement.Parse(AbilityXml));
        Equal(false, AbilityHelper.CanUse(spell, 49, 1000, 0));
        Equal(true, AbilityHelper.CanUse(spell, 50, 1000, 0));
    }

    private static void EnforcesCooldown() {
        var spell = new ItemDesc(2000, XElement.Parse(AbilityXml));
        Equal(false, AbilityHelper.CanUse(spell, 100, 999, 1000));
        Equal(true, AbilityHelper.CanUse(spell, 100, 1000, 1000));

        var next = AbilityHelper.NextUseTime(spell, -1, 1000);
        Equal(1500.0, next, 1e-3);
        Equal(false, AbilityHelper.CanUse(spell, 100, 1499, next));
        Equal(true, AbilityHelper.CanUse(spell, 100, 1500, next));
    }

    private static void CooldownDefaultsToFlash200ms() {
        var cloak = new ItemDesc(2001, XElement.Parse(NoCooldownXml));
        Equal(1200.0, AbilityHelper.NextUseTime(cloak, -1, 1000), 1e-3);
    }

    private static void DetectsShootAbility() {
        var quiver = new ItemDesc(2003, XElement.Parse(ShootXml));
        var spell = new ItemDesc(2000, XElement.Parse(AbilityXml));
        Equal(true, AbilityHelper.HasShootActivate(quiver));
        Equal(false, AbilityHelper.HasShootActivate(spell));
        Equal(false, AbilityHelper.HasShootActivate(null));
    }

    // Space is the default binding; a remapped Settings.Special alias must
    // route through the same public input UserInput.OnKeyDown switches on.
    private static void SpaceAndAliasRouteThroughSpecial() {
        var original = Settings.Special.Key;
        try {
            Settings.Special.Set(Scancode.Spacebar);
            Equal(true, Settings.Special.Equals(Scancode.Spacebar));

            Settings.Special.Set(Scancode.K);
            Equal(true, Settings.Special.Equals(Scancode.K));
            Equal(false, Settings.Special.Equals(Scancode.Spacebar));
        } finally {
            Settings.Special.Set(original);
        }
        Equal(true, Settings.Special.Equals(original));
    }

    // Usable slot-1 ability through the public Player.TryUseAbility input:
    // frame 1 sends UseItem, an immediate frame 2 is gated by cooldown, and
    // a later frame past the 0.5s cooldown sends again.
    private static void UsableAbilitySendsUseItemAcrossFrames() {
        var player = NewPlayer(2000, AbilityXml, mp: 100, objectId: 7);
        player.Position = new Vector2(10, 20);
        var target = new Vector2(13, 22);
        var uses = new List<(int Time, int ObjectId, byte SlotId, float X, float Y, byte UseType)>();
        var shoots = new List<(float Angle, bool Ability, int NumShots, float X, float Y)>();

        Client.OutgoingSink = pkt => {
            if (pkt is UseItem u)
                uses.Add((u.Time, u.SlotObject.ObjectId, u.SlotObject.SlotId, u.ItemUsePos.X, u.ItemUsePos.Y, u.UseType));
            else if (pkt is PlayerShoot s)
                shoots.Add((s.Angle, s.Ability, s.NumShots, s.StartingPos.X, s.StartingPos.Y));
        };
        try {
            Equal(true, player.TryUseAbility(target, 0.5f, new GameTime(1000, 16)));
            Equal(1, uses.Count);
            AssertMoveEpoch(uses[0].Time);
            Equal(7, uses[0].ObjectId);
            Equal((byte)1, uses[0].SlotId);
            Equal(13f, uses[0].X, 1e-5f);
            Equal(22f, uses[0].Y, 1e-5f);
            Equal((byte)UseType.START_USE, uses[0].UseType);
            Equal(0, shoots.Count);

            Equal(false, player.TryUseAbility(target, 0.5f, new GameTime(1000, 16)));
            Equal(1, uses.Count);

            Equal(true, player.TryUseAbility(target, 0.5f, new GameTime(1600, 16)));
            Equal(2, uses.Count);
            AssertMoveEpoch(uses[1].Time);
            Equal(0, shoots.Count);
        } finally {
            Client.OutgoingSink = null;
        }
    }

    // Low-MP ability through the same public input sends nothing.
    private static void LowMpSendsNothingThroughPublicInput() {
        var player = NewPlayer(2000, AbilityXml, mp: 49, objectId: 7);
        player.Position = new Vector2(10, 20);
        var sent = 0;

        Client.OutgoingSink = _ => sent++;
        try {
            Equal(false, player.TryUseAbility(new Vector2(13, 22), 0.5f, new GameTime(1000, 16)));
            Equal(0, sent);
        } finally {
            Client.OutgoingSink = null;
        }
    }

    // Shoot ability through the public input sends UseItem followed by an
    // ability PlayerShoot fanning out the quiver's projectiles.
    private static void ShootAbilitySendsAbilityShoot() {
        var savedAbilityProps = ObjectLibrary.TypeToObjectProps.TryGetValue(2003, out var prevAbilityProps);
        var savedBulletProps = ObjectLibrary.TypeToObjectProps.TryGetValue(2004, out var prevBulletProps);
        var savedBulletType = ObjectLibrary.IdToObjectType.TryGetValue("TestBullet", out var prevBulletType);
        var savedNextId = Map.NextProjectileId;

        var player = NewPlayer(2003, ShootXml, mp: 100, objectId: 9);
        player.Position = new Vector2(10, 20);
        var target = new Vector2(14, 21);
        var uses = new List<(int Time, int ObjectId, byte SlotId, float X, float Y)>();
        var shoots = new List<(float Angle, bool Ability, int NumShots, float X, float Y)>();
        var order = new List<string>();

        ObjectLibrary.TypeToObjectProps[2003] = new ObjectProperties(XElement.Parse(ShootPropsXml));
        ObjectLibrary.TypeToObjectProps[2004] = new ObjectProperties(XElement.Parse(BulletPropsXml));
        ObjectLibrary.IdToObjectType["TestBullet"] = 2004;

        Client.OutgoingSink = pkt => {
            if (pkt is UseItem u) {
                uses.Add((u.Time, u.SlotObject.ObjectId, u.SlotObject.SlotId, u.ItemUsePos.X, u.ItemUsePos.Y));
                order.Add("use");
            } else if (pkt is PlayerShoot s) {
                shoots.Add((s.Angle, s.Ability, s.NumShots, s.StartingPos.X, s.StartingPos.Y));
                order.Add("shoot");
            }
        };
        try {
            Equal(true, player.TryUseAbility(target, 1.0f, new GameTime(2000, 16)));
            Equal(1, uses.Count);
            AssertMoveEpoch(uses[0].Time);
            Equal(9, uses[0].ObjectId);
            Equal((byte)1, uses[0].SlotId);
            Equal(14f, uses[0].X, 1e-5f);
            Equal(21f, uses[0].Y, 1e-5f);
            Equal(1, shoots.Count);
            Equal(true, shoots[0].Ability);
            Equal(3, shoots[0].NumShots);
            Equal(1.0f, shoots[0].Angle, 1e-5f);
            Equal(10f, shoots[0].X, 1e-5f);
            Equal(20f, shoots[0].Y, 1e-5f);
            Equal(2, order.Count);
            Equal("use", order[0]);
            Equal("shoot", order[1]);
        } finally {
            Client.OutgoingSink = null;
            if (savedAbilityProps)
                ObjectLibrary.TypeToObjectProps[2003] = prevAbilityProps!;
            else
                ObjectLibrary.TypeToObjectProps.Remove(2003);
            if (savedBulletProps)
                ObjectLibrary.TypeToObjectProps[2004] = prevBulletProps!;
            else
                ObjectLibrary.TypeToObjectProps.Remove(2004);
            if (savedBulletType)
                ObjectLibrary.IdToObjectType["TestBullet"] = prevBulletType;
            else
                ObjectLibrary.IdToObjectType.Remove("TestBullet");
            Map.Reset();
            Map.NextProjectileId = savedNextId;
        }
    }

    // The server's per-player time gate compares every packet's time against
    // one baseline shared with Move/PlayerShoot. Stamping UseItem with the
    // game clock (a different epoch) was rejected as "Invalid time useitem"
    // and disconnected the client, so the stamp must stay near TickCount.
    private static void UseItemTimeSharesMoveEpoch() {
        var player = NewPlayer(2000, AbilityXml, mp: 100, objectId: 7);
        player.Position = new Vector2(10, 20);
        var uses = new List<int>();

        Client.OutgoingSink = pkt => {
            if (pkt is UseItem u)
                uses.Add(u.Time);
        };
        try {
            Equal(true, player.TryUseAbility(new Vector2(13, 22), 0.5f, new GameTime(1000, 16)));
            Equal(1, uses.Count);
            AssertMoveEpoch(uses[0]);
        } finally {
            Client.OutgoingSink = null;
        }
    }

    private static void AssertMoveEpoch(int packetTime) {
        // Fails for game-clock stamps (off by OS uptime, essentially always
        // far outside this window) and passes for TickCount stamps.
        if (Math.Abs(packetTime - Environment.TickCount) > 5000)
            throw new Exception($"UseItem time {packetTime} is not in the Move/PlayerShoot epoch.");
    }

    private static Player NewPlayer(ushort type, string xml, int mp, int objectId) {
        var player = new Player {
            Mp = mp,
            ObjectId = objectId
        };
        player.Equipment[AbilityHelper.AbilitySlotId] = new ItemDesc(type, XElement.Parse(xml));
        return player;
    }

    private static void Equal<T>(T expected, T actual) {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new Exception($"expected {expected}, got {actual}");
    }

    private static void Equal(double expected, double actual, double tolerance) {
        if (Math.Abs(expected - actual) > tolerance)
            throw new Exception($"expected {expected}, got {actual}");
    }

    private static void Equal(float expected, float actual, float tolerance) {
        if (Math.Abs(expected - actual) > tolerance)
            throw new Exception($"expected {expected}, got {actual}");
    }
}
