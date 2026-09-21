using Alloy.Common;
using AlloyClient;
using AlloyClient.Assets;
using AlloyClient.Game;
using AlloyClient.Game.Objects;
using AlloyClient.Rendering;
using AlloyClient.Rendering.Types.SubTypes;
using AlloyClient.Rendering.VertexData;
using OpenTK.Mathematics;
using System.Collections.Generic;

namespace Alloy.UiLib.Tests;

/// <summary>Condition icons must match Flash (ConditionEffect.as +
/// GameObject.drawConditionIcons): a 16x16 canvas with the 8x8
/// lofiInterface2 glyph at (4,4), black 1.4px outline + black 6px/0.3 glow,
/// laid out edge-to-edge (x = centerX - 16*len/2 + i*16) with the canvas
/// center on the sprite texture top (y = top - 8).
/// Our atlas tile is the bare 8x8 glyph with 1px pad (10 texels). The
/// 0.32-world quad is the Flash 16px canvas with the tile centered 1:1
/// inside (Object.frag maps the inner 10px to the tile, outer 3px stays
/// transparent for the outline + faint glow); centers stay on the 0.32-world
/// canvas pitch and the quad center rides 11.5px above the visible-top
/// anchor (measured Flash center).
/// </summary>
internal static class ConditionIconTests {
    private sealed class FakeParent : RenderBase {
        public override ModelType ModelType => ModelType.PbObject;
        public override bool HasShadow => false;
        public FakeParent() => Position = new Vector3(10f, 20f, 0f);
        public override void SetPosition(float x, float y, float z = 0) => Position = new Vector3(x, y, z);
        public override void SetVisibility(bool visible) { }
        public override void SetDepth(float depth) { }
        public override void SetName(string name) { }
        public override void SetAlpha(float alpha) { }
        public override void Draw(List<VertexObject> targets, double time) { }
    }

    internal static void Run() {
        IconsUseBlackUnshadedOutline();
        IconQuadIsFlashGlyphSize();
        IconRowSitsOnFlashTextureTop();
        IconRowUsesFlashCanvasPitch();
    }

    private static void IconsUseBlackUnshadedOutline() {
        var fx = new TypeEffects(new FakeParent(), null!);
        Equal(Color.Black.ToVector4(), fx.Color.ToVector4());
        Equal(RenderConfig.TypeEffect, fx.Extra.Data.X);
        Equal(RenderConfig.NoShade, fx.Extra.Data.Z);
    }

    private static void IconQuadIsFlashGlyphSize() {
        //0.32-world quad is the Flash 16px canvas; the frag centers the 10px
        //tile 1:1 inside so the 8px glyph stays pixel-identical.
        var fx = new TypeEffects(new FakeParent(), null!);
        Equal(TypeEffects.QuadSize, fx.Scale.X);
        Equal(TypeEffects.QuadSize, fx.Scale.Y);
        Equal(0.32f, TypeEffects.QuadSize);
    }

    private static void IconRowSitsOnFlashTextureTop() {
        //Measured Flash icon center is 11.5px above the visible top;
        //Scale.W carries the quad center.
        var fx = new TypeEffects(new FakeParent(), null!);
        Equal(0.23f, TypeEffects.GlyphLift);
        Equal(-TypeEffects.GlyphLift, fx.Scale.W);
    }

    private static void IconRowUsesFlashCanvasPitch() {
        //Two icons (Speedy + Damaging) must sit 16px (0.32 worlds) apart
        //center-to-center, matching Flash's edge-to-edge 16px canvases.
        Equal(0.32f, TypeEffects.CanvasPitch);

        var hadSpeedy = ConditionEffects.EffectIcons.TryGetValue(ConditionEffect.Speedy, out var prevSpeedy);
        var hadDamaging = ConditionEffects.EffectIcons.TryGetValue(ConditionEffect.Damaging, out var prevDamaging);
        // Icon pitch is measured along world X, so pin the camera like
        // ProjectileRotationTests does instead of reading the live default.
        var prevCamera = Settings.CameraAngle.Value;
        Settings.CameraAngle.Set(0f);
        try {
            ConditionEffects.EffectIcons[ConditionEffect.Speedy] = [new Vector4(0, 0, 1, 1)];
            ConditionEffects.EffectIcons[ConditionEffect.Damaging] = [new Vector4(1, 0, 1, 1)];

            var entity = new Entity();
            entity.EffectBuckets.SetBucket(0, ConditionEffects.TranslateServerMask((1 << 13) | (1 << 16)));
            Equal(2, entity.EffectBuckets.TotalIcons);

            var parent = new FakeParent();
            var fx = new TypeEffects(parent, entity);
            var targets = new List<VertexObject>();
            var yOffset = -0.8f;
            fx.Draw(yOffset, targets, 0);
            Equal(2, targets.Count);
            var x0 = targets[0].Position.X;
            var x1 = targets[1].Position.X;
            Equal(TypeEffects.CanvasPitch, MathF.Abs(x1 - x0), 0.0001f);
            //Row stays centered on the parent.
            Equal(parent.Position.X, (x0 + x1) * 0.5f, 0.0001f);
            Equal(parent.Position.Y + yOffset, targets[0].Position.Y, 0.0001f);
        } finally {
            Settings.CameraAngle.Set(prevCamera);
            if (hadSpeedy) ConditionEffects.EffectIcons[ConditionEffect.Speedy] = prevSpeedy!;
            else ConditionEffects.EffectIcons.Remove(ConditionEffect.Speedy);
            if (hadDamaging) ConditionEffects.EffectIcons[ConditionEffect.Damaging] = prevDamaging!;
            else ConditionEffects.EffectIcons.Remove(ConditionEffect.Damaging);
        }
    }

    private static void Equal<T>(T expected, T actual) {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new Exception($"expected {expected}, got {actual}");
    }

    private static void Equal(float expected, float actual, float tolerance) {
        if (MathF.Abs(expected - actual) > tolerance)
            throw new Exception($"expected {expected} (+/- {tolerance}), got {actual}");
    }
}
