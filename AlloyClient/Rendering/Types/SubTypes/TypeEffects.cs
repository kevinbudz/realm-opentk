using System;
using System.Collections.Generic;
using Alloy.Common;
using AlloyClient.Game.Objects;
using AlloyClient.Rendering.VertexData;
using OpenTK.Mathematics;

namespace AlloyClient.Rendering.Types.SubTypes;

public class TypeEffects : SubRenderBase {

    public override float Height {
        get => 0.12f * 2;
    }

    //Flash icon canvas is 16x16 (0.32 worlds at 50px/tile): the 8x8
    //lofiInterface2 glyph is drawn at (4,4), then a black 1.4px outline
    //(strength 255) plus a black 6px/0.3/strength-2 glow
    //(ConditionEffect.as). Layout is edge-to-edge canvases centered on the
    //sprite texture top: x = centerX - 16*len/2 + i*16, y = top - 8, so the
    //canvas (and glyph) center sits exactly on the top edge.
    //Our atlas tile is the bare 8x8 glyph with 1px pad (10x10 texels). The
    //0.32-world quad is the Flash 16px canvas; Object.frag centers the tile
    //1:1 inside it (inner 10px = 10 texels at zoom 1, glyph a pixel-identical
    //8px) leaving a 3px transparent border each side for the outline + faint
    //glow. Centers stay on the 0.32-world canvas pitch so multi-icon gaps
    //match Flash's 8px glyph gap.
    internal const float QuadSize = 0.32f;
    internal const float CanvasPitch = 0.32f;
    //Flash texture top sits 12px above the visible sprite top
    //(TextureRedrawer.resize's 12px magic border); measured Flash icon center
    //is 11.5px above the visible top (outline rounding), so the quad center
    //rides 11.5px up from our HeightOffset visible-top anchor. Like bars,
    //Scale.W addresses the quad center (positive is down).
    internal const float GlyphLift = 0.23f;

    public TypeEffects(RenderBase parent, Entity entity) {
        Parent = parent;
        Entity = entity;

        UV = new Vector4();
        Scale = new Vector4(QuadSize, QuadSize, 0, -GlyphLift);
        Rotation = new Vector4(0, 1, 1f, -1);
        //Flash draws condition icons untinted with a black outline+glow, the
        //same treatment as its own sprites: black feeds the shader's outline
        //pass, NoShade skips the sprite bottom-shading gradient.
        Extra = new ExtraData(RenderConfig.TypeEffect, RenderConfig.NoShade);
        Color = Color.Black;
    }
    
    public override unsafe void Draw(float yOffset, List<VertexObject> targets, double time) {
        var total = Entity.EffectBuckets.TotalIcons;
        if (total < 1) {
            return;
        }

        Span<Vector4> effects = stackalloc Vector4[total];
        Entity.EffectBuckets.GetEffectsData(effects, (int)(time / 500));
        
        var pos = Parent.Position;
        //Flash centers: (i - (len-1)/2) * 16px. Half-pitch start, full-pitch
        //steps; quad itself stays 10px so the 8px glyphs keep Flash's 8px gap.
        var num = (total - 1) * CanvasPitch * 0.5f;

        var s = MathF.Sin(-Settings.CameraAngle);
        var c = MathF.Cos(-Settings.CameraAngle);

        for (var i = 0; i < total; i++) {
            var x = num * c - yOffset * s;
            var y = num * s + yOffset * c;

            var p = new Vector3(pos.X - x, pos.Y + y, pos.Z);

            targets.Add(new VertexObject(p, effects[i], Scale, Rotation, Extra, Color));
            num -= CanvasPitch;
        }
    }
}