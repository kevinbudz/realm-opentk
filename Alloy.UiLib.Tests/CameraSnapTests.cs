using AlloyClient.Game;
using OpenTK.Mathematics;

namespace Alloy.UiLib.Tests;

/// <summary>The world focuses the camera at ((W-Z)/2, H/2) screen px, where Z is
/// the HUD offset. When that lands on a half-pixel (odd W-Z or odd H), Nearest
/// sampling rasterizes the 1px shader outline asymmetrically (2px ring on one
/// side, 1px on the other, mirroring with sprite flip). The projection must snap
/// the focus to integer pixels so the outline stays symmetric at every size.</summary>
internal static class CameraSnapTests {
    internal static void Run() {
        FocusLandsOnIntegerPixels(800, 600, 200);
        FocusLandsOnIntegerPixels(1280, 800, 266);
        FocusLandsOnIntegerPixels(800, 1000, 333);
        FocusLandsOnIntegerPixels(801, 601, 200);
        FocusLandsOnIntegerPixels(1920, 1081, 360);
    }

    private static void FocusLandsOnIntegerPixels(int w, int h, int z) {
        var pos = new Vector2(50f, 50f);
        var cam = Camera.Update(pos, new Vector3i(w, h, z), 0f, 1f);
        var clip = new Vector4(pos.X, pos.Y, 0f, 1f) * cam.Matrix;
        var px = (clip.X / clip.W + 1f) * 0.5f * w;
        var py = (1f - clip.Y / clip.W) * 0.5f * h;
        if (MathF.Abs(px - MathF.Round(px)) > 1e-3f || MathF.Abs(py - MathF.Round(py)) > 1e-3f) {
            throw new Exception($"focus at ({px}, {py}) for {w}x{h} (hud {z}) is not pixel-snapped");
        }
    }
}
