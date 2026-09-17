using Alloy.Common;
using AlloyClient;
using AlloyClient.Assets;
using AlloyClient.Game;
using AlloyClient.Rendering;
using AlloyClient.Rendering.Types.SubTypes;
using AlloyClient.Rendering.VertexData;
using OpenTK.Mathematics;

namespace Alloy.UiLib.Tests;

/// <summary>World HP/MP bars must keep a fixed world size so they scale with
/// the camera zoom like every other world quad. Compensating for the zoom
/// (constant screen pixels) makes them shrink relative to entities whenever
/// the map is zoomed in.</summary>
internal static class HpBarScaleTests {
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
        WorldSizeIndependentOfZoom();
        PartialFillPinsLeftEdge();
    }

    private static void WorldSizeIndependentOfZoom() {
        var parent = new FakeParent();
        foreach (var zoom in new[] { Settings.MinCameraZoom, 1f, Settings.MaxCameraZoom }) {
            Settings.CameraZoom.Set(zoom);

            var bar = new TypeBar(parent, null!, Color.FromHexRGB(0x6084E0));
            bar.SetFill(1f);
            var targets = new List<VertexObject>();
            bar.Draw(TypeBar.BaseYOffset, targets, 0);
            Equal(2, targets.Count);
            Equal(TypeBar.BackgroundWidth, targets[0].Scale.X);
            Equal(TypeBar.BackgroundHeight, targets[0].Scale.Y);
            Equal(TypeBar.FullWidth, targets[1].Scale.X);
            Equal(TypeBar.BarHeight, targets[1].Scale.Y);
            Equal(TypeBar.BaseYOffset, targets[1].Scale.W);

            var hpBar = new TypeHpBar(parent, null!);
            hpBar.SetFill(1f);
            var hpTargets = new List<VertexObject>();
            hpBar.Draw(TypeBar.BaseYOffset, hpTargets, 0);
            Equal(2, hpTargets.Count);
            Equal(TypeBar.FullWidth, hpTargets[1].Scale.X);
            Equal(TypeBar.BarHeight, hpTargets[1].Scale.Y);
        }
        Settings.CameraZoom.Set(1f);
    }

    private static void PartialFillPinsLeftEdge() {
        var parent = new FakeParent();
        var bar = new TypeBar(parent, null!, Color.FromHexRGB(0x6084E0));
        bar.SetFill(0.5f);
        var targets = new List<VertexObject>();
        bar.Draw(TypeBar.BaseYOffset, targets, 0);
        Equal(2, targets.Count);
        var fill = targets[1];
        Equal(TypeBar.FullWidth * 0.5f, fill.Scale.X);
        // Left edge (-X/2 + Z) stays at the full-width left edge as the bar drains.
        Equal(-TypeBar.HalfWidth, -fill.Scale.X / 2f + fill.Scale.Z, 1e-6f);
    }

    private static void Equal(float expected, float actual, float tolerance) {
        if (MathF.Abs(expected - actual) > tolerance) {
            throw new Exception($"expected {expected}, got {actual}");
        }
    }

    private static void Equal<T>(T expected, T actual) {
        if (!EqualityComparer<T>.Default.Equals(expected, actual)) {
            throw new Exception($"expected {expected}, got {actual}");
        }
    }
}
