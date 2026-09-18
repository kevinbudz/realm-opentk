using System;
using System.Collections.Generic;
using Alloy.Common;
using AlloyClient.Assets;
using AlloyClient.Game.Objects;
using AlloyClient.Rendering.Types.SubTypes;
using AlloyClient.Rendering.VertexData;
using OpenTK.Mathematics;

namespace AlloyClient.Rendering.Types;

public sealed class TypeGameObject : RenderBase {
    
    public override ModelType ModelType {
        get => ModelType.PbObject;
    }

    public override bool HasShadow {
        get => true;
    }

    private readonly TypeName _name;

    private readonly TypeHpBar _hpBar;
    private readonly TypeEffects _effects;

    public TypeGameObject(Entity entity) {
        Entity = entity;
        SetTexture(entity.GetTexture());
        Extra = new ExtraData(RenderConfig.TypeGameObject, RenderConfig.Shade);
        _name = new TypeName(this, entity);
        _hpBar = new TypeHpBar(this, entity);
        _effects = new TypeEffects(this, entity);

        Color = Color.Black;
    }
    
    public override void SetPosition(float x, float y, float z = 0) {
        Position.X = x;
        Position.Y = y;
        Position.Z = z;
    }
    
    public override void SetVisibility(bool visible) {
        Visible = visible;
    }

    public override void SetDepth(float depth) {
        Extra.SortId = depth;
        _name.SetDepth(depth);
        _hpBar.SetDepth(depth);
        _effects.SetDepth(depth);
    }
    
    public override void SetAlpha(float alpha) {
        Extra.Alpha = alpha;
        _name.SetAlpha(alpha);
        _hpBar.SetAlpha(alpha);
        _effects.SetAlpha(alpha);
    }

    public override void SetName(string name) { }

    public override void Draw(List<VertexObject> targets, double time) {
        var s = MathF.Sin(-Entity.Rotation);
        var c = MathF.Cos(-Entity.Rotation);
        var k = Entity.Size / 100f;
        var f = Entity.Flipped ? 1f : -1f;
        Rotation = new Vector4(s, c, k, f);
        
        Entity.HeightOffset = GetVisibleTopOffset(k);

        // Flash-parity sinking: world-space clip packed in Mask1 (x = drop, y =
        // rise), applied by Object.vert so the sprite sinks to Flash depth.
        var sink = Entity.GetSinkClip();
        var sprite = new VertexObject(Position, UV, Scale, Rotation, Extra, Color) {
            Mask1 = new Vector4(sink.Drop, sink.Rise, 0f, 0f)
        };
        targets.Add(sprite);

        if (Entity.Properties.Static) {
            return;
        }

        if (TypeHpBar.CanDrawForGameObject(Entity)) {
            var maximumHp = Math.Max(Entity.MaxHp, Entity.Hp);
            _hpBar.SetFill(1f * Entity.Hp / maximumHp);
            _hpBar.Draw(TypeBar.BaseYOffset, targets, time);
        }

        // Condition icons ride the sunk sprite top like Flash (vS_[1]).
        _effects.Draw(Entity.HeightOffset + sink.Drop, targets, time);
    }

    public override void DrawShadow() {
        if (Entity.Size == 0) return;
        Render.DrawShadow(new ShadowData(Position.Xy, 1f, Color.Black));
    }
}
