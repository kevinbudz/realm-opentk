using AlloyClient.Game.Objects;
using AlloyClient.Rendering.Types.SubTypes;

namespace Alloy.UiLib.Tests;

/// <summary>Flash parity for sinking (GameObject.draw h2 + Player.onMove) and
/// the world HP bar offset (GameObject.DEFAULT_HP_BAR_Y_OFFSET).</summary>
internal static class SinkParityTests {
    internal static void Run() {
        TileSinkPixels();
        SinkLevelAddsToTileSink();
        FlyingAndProtectCancelSink();
        SinkPixelToWorldScale();
        SinkRiseCorrectsMargins();
        HpBarCenterMatchesFlash();
        HpBarSizeMatchesFlash();
    }

    // Square.sink_: 12px on plain tiles, 6px on redrawn (blended) tiles.
    private static void TileSinkPixels() {
        Equal(12 * Entity.SinkPixelToWorld, Entity.ComputeSinkHeight(false, false, true, false, 0), 1e-6f);
        Equal(6 * Entity.SinkPixelToWorld, Entity.ComputeSinkHeight(false, false, true, true, 0), 1e-6f);
        Equal(0f, Entity.ComputeSinkHeight(false, false, false, false, 0), 1e-6f);
    }

    // Player.onMove accumulates up to MAX_SINK_LEVEL (18) on top of the tile sink.
    private static void SinkLevelAddsToTileSink() {
        Equal(18 * Entity.SinkPixelToWorld, Entity.ComputeSinkHeight(flying: false, protectFromSink: false, tileSink: false, tileOverlay: false, sinkLevel: 18), 1e-6f);
        Equal(30 * Entity.SinkPixelToWorld, Entity.ComputeSinkHeight(flying: false, protectFromSink: false, tileSink: true, tileOverlay: false, sinkLevel: 18), 1e-6f);
    }

    // h2 is zeroed while flying or standing on a ProtectFromSink object.
    private static void FlyingAndProtectCancelSink() {
        Equal(0f, Entity.ComputeSinkHeight(flying: true, protectFromSink: false, tileSink: true, tileOverlay: false, sinkLevel: 18), 1e-6f);
        Equal(0f, Entity.ComputeSinkHeight(flying: false, protectFromSink: true, tileSink: true, tileOverlay: false, sinkLevel: 18), 1e-6f);
    }

    // TextureRedrawer 5x combined with 0.1 world units per atlas texel: full
    // sink (18 + 12px) covers 75% of an 8px player sprite, exactly like Flash
    // (30 of 40 resized px).
    private static void SinkPixelToWorldScale() {
        Equal(0.02f, Entity.SinkPixelToWorld, 1e-6f);
        Equal(0.75f, Entity.ComputeSinkHeight(false, false, true, false, 18) / 0.8f, 1e-6f);
    }

    // Alloy pads atlas sprites 1 texel per side while Flash's redrawn texture
    // carries 12px top / 1px bottom margins: after the head-side drop, the
    // feet side must also rise by the pad difference (5k-1 px) so the visible
    // rows and the below-feet line match Flash.
    private static void SinkRiseCorrectsMargins() {
        var clip = Entity.ComputeSinkClip(flying: false, protectFromSink: false, tileSink: true, tileOverlay: false, sinkLevel: 18, sizeScale: 1f);
        Equal(30 * Entity.SinkPixelToWorld, clip.Drop, 1e-6f);
        Equal(4 * Entity.SinkPixelToWorld, clip.Rise, 1e-6f);

        var none = Entity.ComputeSinkClip(false, false, false, false, 0, 1f);
        Equal(0f, none.Drop, 1e-6f);
        Equal(0f, none.Rise, 1e-6f);

        var flying = Entity.ComputeSinkClip(true, false, true, false, 18, 1f);
        Equal(0f, flying.Drop, 1e-6f);
        Equal(0f, flying.Rise, 1e-6f);
    }

    // Flash HP bar: fill top 5px below the feet with a 1.2px background pad,
    // so the bar center sits 7.5px = 0.15 tiles below the feet at zoom 1
    // (yOffset addresses the bar center).
    private static void HpBarCenterMatchesFlash() {
        Equal(7.5f / 50f, TypeBar.BaseYOffset, 1e-6f);
    }

    // Flash HP bar 1:1 at zoom 1 (50px/tile): 40x5 fill, 42.4x7.4 background.
    private static void HpBarSizeMatchesFlash() {
        Equal(40f / 50f, TypeBar.FullWidth, 1e-6f);
        Equal(5f / 50f, TypeBar.BarHeight, 1e-6f);
        Equal(42.4f / 50f, TypeBar.BackgroundWidth, 1e-6f);
        Equal(7.4f / 50f, TypeBar.BackgroundHeight, 1e-6f);
    }

    private static void Equal(float expected, float actual, float tolerance) {
        if (MathF.Abs(expected - actual) > tolerance) {
            throw new Exception($"expected {expected}, got {actual}");
        }
    }
}
