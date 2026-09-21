using System;
using Alloy.Engine;
using AlloyClient.Game;
using AlloyClient.Game.Objects;
using Alloy.UiLib.BuiltIn;
using Alloy.UiLib.Core;
using Alloy.UiLib.Extra;
using AlloyClient.Ui.Flash;
using AlloyClient.Utils;
using OpenTK.Mathematics;

namespace AlloyClient.Ui.Character;

public class CharacterStatusText : Sprite {
    // Flash parity (mapoverlay/CharacterStatusText.as): damage numbers drift
    // at most 20px from their spawn point over their lifetime.
    private const int MaxDrift = 20;

    // Flash parity (offset_ y - 20): the text floats a fixed 20 screen px
    // above the sprite top, independent of camera zoom.
    private const int SpawnYOffset = 20;

    // Flash parity (GameObject.damage): normal hits render red, hits that
    // pierce armor (armor broken, armor-piercing shot, ground damage) purple.
    public const uint NormalColor = 0xFF0000;
    public const uint PiercedColor = 0x9000FF;

    // Wire id of armor broken in Damage/Aoe effect bytes. These carry the
    // server's ConditionEffectIndex (realm-server/Common/Descriptors.cs:
    // Nothing = 0 .. ArmorBroken = 24), NOT this client's Dead-shifted
    // ConditionEffect enum (ArmorBroken = 27).
    private const byte ServerArmorBrokenEffectId = 24;

    // Flash parity (CharacterStatusText: t.filters = [new GlowFilter(0, 1, 4,
    // 4, 2, 1)]): the black halo that lifts the numbers off the background.
    // An explicit filter takes precedence over the legacy MSDF outline.
    public static DropShadowFilter DamageGlow => FlashTextFilters.StrongOutline;

    private readonly Entity _owner;
    private readonly double _lifetime;
    private readonly double _endTime;
    private readonly double _startTime;
    private readonly bool _randomized;

    private bool _spawned;
    private float _xOffset;
    private float _driftDir;

    public CharacterStatusText(Entity en, string text, uint color, double lifetime, double startTime, bool randomized = false) {
        _owner = en;
        _lifetime = lifetime;
        _endTime = startTime + lifetime;
        _startTime = startTime;
        _randomized = randomized;

        // Flash parity (new SimpleText(24, color) + setBold(true) + the black
        // GlowFilter above).
        var txtConfig = new TextConfig {
            Text = text,
            Color = color,
            MaxWidth = 120,
            FontSize = 24,
            FontType = FontType.Bold,
            OutlineThickness = 0,
            DropShadow = DamageGlow
        };

        AddChild(new SimpleText(txtConfig));

        Visible = false;
        // Flash centers the text on the spawn point (t.x = -w/2, t.y = -h/2).
        SetAnchor(UiAnchor.Middle);
    }

    // Flash parity (GameObject.damage pierced check): purple when the target
    // is armor broken or the shot itself pierces armor.
    public static uint ResolveDamageColor(Entity target, bool armorPiercing) =>
        target.HasConditionEffect(ConditionEffect.ArmorBroken) || armorPiercing ? PiercedColor : NormalColor;

    public static bool IsArmorBrokenEffect(byte serverEffectId) => serverEffectId == ServerArmorBrokenEffectId;

    // Flash parity (GameObject.damage StunImmune gate): the only immunity
    // this server enforces in ApplyConditionEffect is Stunned blocked by
    // StunImmune. Other betterskillys immunities (Slowed/ArmorBroken/Dazed/
    // Paralyze/Petrified/Curse) have no server counterpart and are ignored.
    // Pure for testability.
    public static bool IsImmuneBlocked(ConditionEffect effect, bool hasStunImmune) =>
        effect == ConditionEffect.Stunned && hasStunImmune;

    // Flash parity (GameObject.damage effects loop): apply each server effect
    // id immediately and show its red name 3000ms, staggered 500ms; already-
    // present effects are skipped without text. Nothing (0) and
    // betterskillys-only ids past Hexed never arrive and are dropped.
    public static void ApplyDamageEffects(Entity target, byte[] effectIds, int count) {
        if (target == null || effectIds == null || count <= 0)
            return;
        var offsetTime = 0;
        var stunImmune = target.HasConditionEffect(ConditionEffect.StunImmune);
        var n = Math.Min(count, effectIds.Length);
        for (var i = 0; i < n; i++)
            ApplyDamageEffect(target, effectIds[i], ref offsetTime, stunImmune);
    }

    // Single-effect variant for the Aoe packet's lone effect byte.
    public static void ApplyDamageEffect(Entity target, byte serverEffectId) {
        if (target == null)
            return;
        var offsetTime = 0;
        ApplyDamageEffect(target, serverEffectId, ref offsetTime, target.HasConditionEffect(ConditionEffect.StunImmune));
    }

    private static void ApplyDamageEffect(Entity target, byte serverEffectId, ref int offsetTime, bool stunImmune) {
        if (!ConditionEffects.TryMapServerEffect(serverEffectId, out var effect))
            return;
        if (IsImmuneBlocked(effect, stunImmune)) {
            NotificationLayer.AddStatusText(target, "Stunned Immune", NormalColor, 3000, 0);
            return;
        }
        // Flash parity (case QUIET): the local player's MP zeroes at once.
        if (effect == ConditionEffect.Quiet && target is Player player && ReferenceEquals(target, Map.LocalPlayer))
            player.Mp = 0;
        if (target.HasConditionEffect(effect))
            return;
        target.AddConditionEffect(effect);
        NotificationLayer.AddStatusText(target, ConditionEffects.GetEffectName(effect), NormalColor, 3000, offsetTime);
        offsetTime += 500;
    }

    // Flash parity (draw: drift = dt / lifetime * MAX_DRIFT): the number
    // floats 20px upward over its life. Pure for testability.
    public static double ComputeVerticalDrift(double elapsed) => elapsed * MaxDrift;

    // Flash parity (draw: alpha = 1 / (drift / 15)): the number stays fully
    // opaque for most of its life and pops out instead of fading linearly.
    // Pure for testability.
    public static float ComputeAlpha(double elapsed) {
        var drift = ComputeVerticalDrift(elapsed);
        return (float)Math.Min(1.0, 15.0 / Math.Max(drift, 0.001));
    }

    // Map-scale parity (SpeechBubble.Update): the number renders at map
    // scale, so scroll zoom grows/shrinks it like the world. Pure for
    // testability; Update applies it to Scale each frame.
    public static float ComputeScale(float cameraZoom) => cameraZoom;

    public bool Update(in GameTime gameTime, in Camera camera) {
        if (_owner == null || _endTime < gameTime.TotalMs) {
            return false;
        }

        // Flash parity (draw returns false when go.map_ == null): text dies
        // with its target instead of lingering at its last position.
        if (!Map.Entities.TryGetValue(_owner.ObjectId, out var live) || !ReferenceEquals(live, _owner)) {
            return false;
        }

        Visible = _startTime < gameTime.TotalMs;

        if (!_spawned && Visible) {
            _spawned = true;
            if (_randomized) {
                // Flash parity (randomized=true): spawn within half the
                // sprite width and drift sideways, all in constant screen px.
                _xOffset = Random.Shared.PlusMinus(HalfWidthPx(camera));
                _driftDir = Random.Shared.PlusMinus(MaxDrift);
            }
        }

        var elapsed = (gameTime.TotalMs - _startTime) / _lifetime;

        var pos = camera.WorldToScreen(new Vector3(_owner.X, _owner.Y, _owner.Z - _owner.HeightOffset), Stage.Dimensions);
        Scale = new Vector2(ComputeScale(Settings.CameraZoom));
        var verticalDrift = ComputeVerticalDrift(elapsed);

        if (_randomized) {
            // Flash parity: the sideways drift pushes away from the spawn
            // side (offset.x > 0 ? offset.x + drift : offset.x - drift).
            var horizontalDrift = elapsed * _driftDir;
            X = pos.X + (int)(_xOffset > 0 ? _xOffset + horizontalDrift : _xOffset - horizontalDrift);
        } else {
            X = pos.X;
        }
        Y = pos.Y - SpawnYOffset - (int)verticalDrift;

        Alpha = ComputeAlpha(elapsed);
        // Player Alpha option: damage text attached to other players fades
        // with the slider; text on the local player and on enemies stays.
        if (_owner is Player && _owner != Map.LocalPlayer) {
            Alpha *= Settings.GetOtherPlayerAlpha(false);
        }
        return true;
    }

    // Half the sprite's on-screen width, mirroring Flash's spawn range of
    // texture_.width / 2 px. Derived from the sprite top offset (full sprite
    // height in world units) and the texture aspect.
    private float HalfWidthPx(in Camera camera) {
        var pixelsPerTile = Stage.StageHeight / Math.Max(camera.VisibleTileRadius.Y * 2, float.Epsilon);
        var rawW = _owner.Texture.RawW();
        var rawH = _owner.Texture.RawH();
        var aspect = rawW > 0 && rawH > 0 ? (float)rawW / rawH : 1f;
        return Math.Abs(_owner.HeightOffset) * (float)pixelsPerTile * aspect / 2;
    }
}
