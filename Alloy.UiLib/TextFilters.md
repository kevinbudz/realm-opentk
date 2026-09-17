# Text shadows and outlines

`SimpleText`, `TextInput`, and the client's `TextButtonConfig` accept a
`DropShadow` from `Alloy.UiLib.Extra`. A filter applies to the whole text run,
including all lines. It leaves line wrapping, measured size, anchoring, and hit
areas unchanged; parent clipping still clips the shadow.

```csharp
// A lightly displaced shadow.
var label = new SimpleText(new TextConfig {
    Text = "Realm of the Mad God",
    FontSize = 24,
    Color = 0xFFFFFF,
    DropShadow = new DropShadowFilter(
        distance: 2, angle: 45, alpha: 0.35f, blurX: 4, blurY: 4)
});

// A darker outline: zero displacement and amplified blurred coverage.
label.DropShadow = new DropShadowFilter(
    distance: 0, angle: 0, blurX: 4, blurY: 4, strength: 2);

// Restore the unfiltered text / legacy outline path.
label.DropShadow = null;
```

The constructor follows Flash's parameter order and defaults:
`distance=4, angle=45, color=0, alpha=1, blurX=4, blurY=4, strength=1,
quality=1, inner=false, knockout=false, hideObject=false`.
Angles are clockwise in a downward-Y UI; 90 degrees moves the shadow down.
Negative distance reverses the direction. Color is packed `0xRRGGBB`.
Blur widths and distance use local design pixels. `quality` is the number of
separable box-blur passes (0 disables blur; 1–3 is normally sufficient).
`inner` clips an inverted shadow to the text, `knockout` removes the text fill,
and `hideObject` shows only the shadow without cutting out an outer shadow.

Filters are immutable and safe to share. Assign a new filter to change settings.
Finite values are required; alpha is clamped to 0–1, blur and strength to 0–255,
and quality to 0–15. An explicit filter takes precedence over
`OutlineThickness`/`OutlineColor`; the old path remains available when it is null.
`TextInput` filters only its visible glyphs, excluding the caret and background.

The client provides `FlashTextFilters.Default` (4×4, strength 1),
`StrongOutline` (4×4, strength 2), and `Soft` (12×12, alpha 0.5), taken from its
ActionScript sources. HUD labels and character names use `Default`, potion
counts use `StrongOutline`, and menu text uses `Soft`.

## Rendering and limits

The MTSDF atlas still supplies glyph coverage. Filtered text rasterizes that
coverage into a padded mask for the complete text run and blurs it horizontally
and vertically for the halo. Outer filters (every client use: `Default`,
`StrongOutline`, `Soft`) then draw the halo from the mask and the glyph run
itself through the normal MSDF path, composited shadow-behind over two passes;
this equals the old premultiplied composite, but glyph edges and AA match
unfiltered text exactly at any placement or magnification. This avoids
atlas-cell clipping and lets adjacent glyph shadows blend before applying
strength. The filter color is independent of text color. Sprite/parent alpha
and color transforms apply to the finished result; an explicit filter still
takes precedence over the legacy outline/glow.

Masks are cached by owner, geometry revision, filter value, and quantized
world scale. Repositioning, recoloring, and fading do not regenerate them;
resizing the window or otherwise magnifying the label does, so a magnified
halo keeps roughly one mask texel per screen pixel. The scale is measured
from the sprite's world transform, rounded up to quarter steps, clamped to
1–4×, and applied per axis; blur radii, offsets, and the final quad stay in
local design pixels. The cache evicts least recently used entries at
128 labels or 32 MiB (counted at supersampled size), and disposes GPU textures
on eviction, font replacement, and UI shutdown. Filter draws flush the UI
batch; cache misses also incur mask and blur passes. Texture units 14 and 15
are reserved for this renderer. Very large masks exceeding the GPU texture
limit or the 32 MiB budget are rejected with an explanatory exception.

This is a Flash-style implementation, not a pixel-identical Flash emulator.
Rasterization uses the current font atlas, and overlapping glyph coverage uses
the maximum coverage. Masks scale and rotate with the sprite. Labels under
16pt use the anisotropic MSDF path, whose smoothing width is floored at one
screen pixel so magnified small text keeps full antialiasing instead of
thinning into stairs. Flash's filter transformation rules and exact blur
rounding differ. For large text, prefer increasing `FontSize` over magnifying
a small filtered label.

API reference: [AIR DropShadowFilter](https://airsdk.dev/reference/actionscript/3.0/flash/filters/DropShadowFilter.html).

## Validation

The standalone `Alloy.UiLib.Tests` console project exercises parameter handling
and GPU mask/filter rendering using a synthetic distance-field atlas. On Linux
it creates a surfaceless EGL OpenGL context, so it does not need a game server or
the client's generated content. Run from the solution directory:

```sh
dotnet build Alloy.UiLib.Tests -m:1
dotnet run --project Alloy.UiLib.Tests --no-build
```

After building the client content, optionally append
`-- --preview AlloyClient/bin/Debug/net10.0/Content /tmp/text-preview.ppm`
to render a comparison sheet using the actual Myriad font. GPU tests report a
skip when a Linux EGL context is unavailable.
