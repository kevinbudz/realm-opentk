using System;
using System.Collections.Generic;
using Alloy.Common;
using AlloyClient.Assets;
using AlloyClient.Game;
using AlloyClient.Game.Objects;
using AlloyClient.Rendering.Types.SubTypes;
using AlloyClient.Rendering.VertexData;
using OpenTK.Mathematics;

namespace AlloyClient.Rendering.Types;

public sealed class TypePlayer : RenderBase {
    
    public override ModelType ModelType {
        get => ModelType.PbObject;
    }

    public override bool HasShadow {
        get => true;
    }
    
    private readonly Player _player;
    
    private TypeName _typeName;
    private readonly TypeHpBar _hpBar;
    private readonly TypeBar _mpBar;
    private readonly TypeEffects _effects;

    public TypePlayer(Player player) {
        Entity = player;
        _player = player;
        
        SetTexture(player.GetTexture());
        Extra = new ExtraData(RenderConfig.TypeGameObject, RenderConfig.Shade);
        
        _typeName = new TypeName(this, player);
        _hpBar = new TypeHpBar(this, player);
        _mpBar = new TypeBar(this, player, Color.FromHexRGB(0x6084E0));
        _effects = new TypeEffects(this, player);
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
        _typeName.SetDepth(depth);
        _hpBar.SetDepth(depth);
        _mpBar.SetDepth(depth);
        _effects.SetDepth(depth);
    }
    
    public override void SetAlpha(float alpha) {
        Extra.Alpha = alpha;
        _typeName.SetAlpha(alpha);
        _hpBar.SetAlpha(alpha);
        _mpBar.SetAlpha(alpha);
        _effects.SetAlpha(alpha);
    }

    public override void SetName(string name) {
        _typeName.Name = name;
        _typeName.SetTextures();
    }

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
        var y = TypeBar.BaseYOffset;
        if (_player != Map.LocalPlayer) {
            _typeName.Draw(y, targets, time);
            y += _typeName.Height;
        }

        var drawHealthBar = _player.MaxHp > 0 && TypeHpBar.CanDrawForPlayer(_player);
        if (drawHealthBar) {
            var maximumHp = Math.Max(_player.MaxHp, _player.Hp);
            _hpBar.SetFill(1f * _player.Hp / maximumHp);
            _hpBar.Draw(y, targets, time);
            y += _hpBar.Height;
        }

        if (Settings.DrawMpBar && _player.MaxMp > 0) {
            _mpBar.SetFill(1f * _player.Mp / _player.MaxMp);
            _mpBar.Draw(y, targets, time);
        }

        // Condition icons ride the sunk sprite top like Flash (vS_[1]).
        _effects.Draw(Entity.HeightOffset + sink.Drop, targets, time);
        
    }

    public override void DrawShadow() {
        if (Entity.Size == 0) return;
        Render.DrawShadow(new ShadowData(Position.Xy, 1f, Color.Black));
    }
}
