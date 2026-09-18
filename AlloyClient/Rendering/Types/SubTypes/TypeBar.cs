using System;
using System.Collections.Generic;
using Alloy.Common;
using AlloyClient.Game.Objects;
using AlloyClient.Rendering.VertexData;
using OpenTK.Mathematics;

namespace AlloyClient.Rendering.Types.SubTypes;

public class TypeBar : SubRenderBase {
    // World-space sizes shared with the other branches. Bars intentionally
    // scale with the camera zoom like every other world quad (sprites,
    // names, effects) instead of compensating for it, so they keep a stable
    // size relative to the entities they belong to.
    // Flash 1:1 at zoom 1 (50px/tile): 40x5 fill (w=20,h=5), 42.4x7.4
    // background (1.2px pad).
    public const float FullWidth = 0.8f;
    public const float HalfWidth = FullWidth / 2f;
    public const float BarHeight = 0.1f;
    public const float BackgroundWidth = 0.848f;
    public const float BackgroundHeight = 0.148f;
    // Flash parity: DEFAULT_HP_BAR_Y_OFFSET puts the fill top 5px below the
    // feet and the 1.2px background pad centers the bar at 7.5px = 0.15 tiles
    // at zoom 1 (yOffset addresses the bar center).
    public const float BaseYOffset = 0.15f;
    public const float RowSpacing = 0.12f * 2;

    public override float Height => RowSpacing;

    private readonly Color _backgroundColor = Color.FromHexRGB(0x111111);
    private Vector4 _backgroundScale;
    private float _fill;

    public TypeBar(RenderBase parent, Entity entity, Color color) {
        Parent = parent;
        Entity = entity;
        Color = color;

        UV = Vector4.Zero;
        _backgroundScale = Vector4.Zero;
        Rotation = new Vector4(0, 1, 1, -1);
        Extra = new ExtraData(RenderConfig.TypeBar, RenderConfig.NoShade);
    }

    public void SetFill(float percent) {
        _fill = Math.Clamp(percent, 0f, 1f);
    }

    public override void Draw(float yOffset, List<VertexObject> targets, double time) {
        _backgroundScale.X = BackgroundWidth;
        _backgroundScale.Y = BackgroundHeight;
        _backgroundScale.Z = 0f;
        _backgroundScale.W = yOffset;
        var backgroundExtra = Extra;
        backgroundExtra.SortId += 0.000001f;
        targets.Add(new VertexObject(Parent.Position, UV, _backgroundScale, Rotation, backgroundExtra, _backgroundColor));

        if (_fill <= 0f) {
            return;
        }

        Scale.X = FullWidth * _fill;
        Scale.Y = BarHeight;
        Scale.Z = HalfWidth * (_fill - 1f);
        Scale.W = yOffset;
        targets.Add(new VertexObject(Parent.Position, UV, Scale, Rotation, Extra, Color));
    }
}
