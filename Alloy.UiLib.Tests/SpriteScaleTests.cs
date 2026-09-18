using Alloy.Common.Structs;
using AlloyClient.Assets;
using AlloyClient.Rendering;
using AlloyClient.Rendering.VertexData;
using OpenTK.Mathematics;

namespace Alloy.UiLib.Tests;

/// <summary>World sprites must keep Flash texel parity: TextureRedrawer scales
/// sprites 5x and the camera maps one tile to 50 screen px at zoom 1, so each
/// atlas texel (padding included) covers exactly 0.1 tiles = 5 screen px.
/// Fractional density makes Nearest sampling alternate texel widths on screen
/// ("stretched pixels") at every zoom and window size, and non-square source
/// sprites must keep their aspect instead of being forced square.</summary>
internal static class SpriteScaleTests {
    private sealed class FakeSprite : RenderBase {
        public override ModelType ModelType => ModelType.PbObject;
        public override bool HasShadow => false;
        public override void SetPosition(float x, float y, float z = 0) => Position = new Vector3(x, y, z);
        public override void SetVisibility(bool visible) { }
        public override void SetDepth(float depth) { }
        public override void SetName(string name) { }
        public override void SetAlpha(float alpha) { }
        public override void Draw(List<VertexObject> targets, double time) { }
    }

    internal static void Run() {
        SquareSpriteHasIntegerTexelDensity();
        NonSquareSpriteKeepsAspect();
        AttackFrameStaysDoubleWide();
    }

    // 8x8 player sprite + 1px atlas padding = 10x10 raw texels -> 1.0x1.0 tiles.
    private static void SquareSpriteHasIntegerTexelDensity() {
        var sprite = new FakeSprite();
        sprite.SetTexture(AtlasData.FromRaw(0, 0, 10, 10));
        Equal(1f, sprite.Scale.X, 1e-6f);
        Equal(1f, sprite.Scale.Y, 1e-6f);
        // 50 screen px per tile at zoom 1 -> exactly 5 px per texel on both axes.
        Equal(5f, sprite.Scale.X / 10f * 50f, 1e-4f);
        Equal(5f, sprite.Scale.Y / 10f * 50f, 1e-4f);
    }

    // 16x8 encounter frame + padding = 18x10 raw texels -> 1.8x1.0 tiles,
    // i.e. the same texel density on both axes, not a square quad.
    private static void NonSquareSpriteKeepsAspect() {
        var sprite = new FakeSprite();
        sprite.SetTexture(AtlasData.FromRaw(0, 0, 18, 10));
        Equal(1.8f, sprite.Scale.X, 1e-6f);
        Equal(1f, sprite.Scale.Y, 1e-6f);
        Equal(sprite.Scale.X / 18f, sprite.Scale.Y / 10f, 1e-6f);
    }

    // Double-wide attack frame keeps square texels, not a square quad.
    private static void AttackFrameStaysDoubleWide() {
        var sprite = new FakeSprite();
        sprite.SetTexture(AtlasData.FromRaw(0, 0, 18, 10), true);
        Equal(1.8f, sprite.Scale.X, 1e-6f);
        Equal(1f, sprite.Scale.Y, 1e-6f);
        Equal(sprite.Scale.X / 18f, sprite.Scale.Y / 10f, 1e-6f);
    }

    private static void Equal(float expected, float actual, float tolerance) {
        if (MathF.Abs(expected - actual) > tolerance) {
            throw new Exception($"expected {expected}, got {actual}");
        }
    }
}
