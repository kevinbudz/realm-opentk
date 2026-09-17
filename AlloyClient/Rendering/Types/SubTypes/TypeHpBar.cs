using System;
using System.Collections.Generic;
using Alloy.Common;
using AlloyClient.Game;
using AlloyClient.Game.Objects;
using AlloyClient.Rendering.VertexData;
using OpenTK.Mathematics;

namespace AlloyClient.Rendering.Types.SubTypes;

public class TypeHpBar : SubRenderBase {
    private readonly static Color HighFill = Color.FromHexRGB(0x10FF00);
    private readonly static Color LowFill = Color.FromHexRGB(0xFF8010);
    private readonly static Color MediumFill = Color.FromHexRGB(0xE01010);

    public override float Height => TypeBar.RowSpacing;

    private readonly Color _backgroundColor = Color.FromHexRGB(0x111111);
    private Vector4 _backgroundScale;
    private float _fill;

    public TypeHpBar(RenderBase parent, Entity entity) {
        Parent = parent;
        Entity = entity;

        UV = Vector4.Zero;
        _backgroundScale = Vector4.Zero;
        Rotation = new Vector4(0, 1, 1, -1);
        Extra = new ExtraData(RenderConfig.TypeBar, RenderConfig.NoShade);
    }

    public void SetFill(float percent) {
        _fill = Math.Clamp(percent, 0f, 1f);
        Color = _fill < 0.5f ? _fill >= 0.2f ? LowFill : MediumFill : HighFill;
    }

    public override void Draw(float yOffset, List<VertexObject> targets, double time) {
        _backgroundScale.X = TypeBar.BackgroundWidth;
        _backgroundScale.Y = TypeBar.BackgroundHeight;
        _backgroundScale.Z = 0f;
        _backgroundScale.W = yOffset;
        var backgroundExtra = Extra;
        backgroundExtra.SortId += 0.000001f;
        targets.Add(new VertexObject(Parent.Position, UV, _backgroundScale, Rotation, backgroundExtra, _backgroundColor));

        if (_fill <= 0f) {
            return;
        }

        Scale.X = TypeBar.FullWidth * _fill;
        Scale.Y = TypeBar.BarHeight;
        Scale.Z = TypeBar.HalfWidth * (_fill - 1f);
        Scale.W = yOffset;
        targets.Add(new VertexObject(Parent.Position, UV, Scale, Rotation, Extra, Color));
    }

    public static bool CanDrawForPlayer(Entity entity) {
        if (entity.HasConditionEffect(ConditionEffect.Invincible) || entity.HasConditionEffect(ConditionEffect.Invulnerable)) {
            return false;
        }

        return CanDrawFromOption(entity);
    }

    public static bool CanDrawForGameObject(Entity entity) {
        if (entity.MaxHp <= 0 || entity.Properties.NoMiniMap || (!entity.Properties.IsEnemy && !entity.Properties.IsAlly)) {
            return false;
        }

        if (entity.HasConditionEffect(ConditionEffect.Invincible) || entity.HasConditionEffect(ConditionEffect.Invulnerable) ||
            entity.HasConditionEffect(ConditionEffect.Paused) || entity.HasConditionEffect(ConditionEffect.Invisible)) {
            return false;
        }

        return CanDrawFromOption(entity);
    }

    private static bool CanDrawFromOption(Entity entity) {
        return Settings.HealthBars.Value switch {
            0 => false,
            1 => true,
            2 => entity == Map.LocalPlayer,
            3 => entity == Map.LocalPlayer || !entity.Properties.IsPlayer,
            _ => true,
        };
    }
}
