using System;

namespace Alloy.UiLib.Extra;

/// <summary>
/// Immutable Flash-style text shadow. Distances and blur widths are in local UI
/// pixels; angle is clockwise in degrees (90 points down). Strength amplifies the
/// blurred alpha, so a zero-distance, high-strength shadow produces an outline.
/// </summary>
/// <remarks>
/// The renderer blurs the complete text mask before compositing. Quality selects
/// repeated box-blur passes, not supersampling. Filters do not change text layout,
/// wrapping, anchors, or hit areas. This recreates Flash's controls and compositing
/// model, but does not promise pixel-identical Flash rasterization.
/// </remarks>
public sealed record DropShadowFilter {
    public float Distance { get; }
    public float Angle { get; }
    public uint Color { get; }
    public float Alpha { get; }
    public float BlurX { get; }
    public float BlurY { get; }
    public float Strength { get; }
    public int Quality { get; }
    public bool Inner { get; }
    public bool Knockout { get; }
    public bool HideObject { get; }

    public DropShadowFilter(float distance = 4, float angle = 45, uint color = 0,
        float alpha = 1, float blurX = 4, float blurY = 4, float strength = 1,
        int quality = 1, bool inner = false, bool knockout = false, bool hideObject = false) {
        Distance = Finite(distance, nameof(distance));
        Angle = Finite(angle, nameof(angle)) % 360;
        Color = color & 0xFFFFFF;
        Alpha = Math.Clamp(Finite(alpha, nameof(alpha)), 0, 1);
        BlurX = Math.Clamp(Finite(blurX, nameof(blurX)), 0, 255);
        BlurY = Math.Clamp(Finite(blurY, nameof(blurY)), 0, 255);
        Strength = Math.Clamp(Finite(strength, nameof(strength)), 0, 255);
        Quality = Math.Clamp(quality, 0, 15);
        Inner = inner;
        Knockout = knockout;
        HideObject = hideObject;
    }

    private static float Finite(float value, string name) {
        if (!float.IsFinite(value)) {
            throw new ArgumentOutOfRangeException(name, "Filter values must be finite.");
        }

        return value;
    }
}
