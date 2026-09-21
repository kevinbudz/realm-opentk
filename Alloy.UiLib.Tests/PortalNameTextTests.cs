using Alloy.Common;
using Alloy.Common.Structs;
using Alloy.ContentReader;
using Alloy.UiLib.Core;
using Alloy.UiLib.Data;
using AlloyClient.Game.Objects;
using AlloyClient.Rendering.Types.SubTypes;

namespace Alloy.UiLib.Tests;

// Flash parity for world floating names (GameObject.generateNameText):
// SimpleText(16, white, bold) with no letter-spacing. Regression cover for
// the Nexus portal label rendering small, gold, and with a gap between
// every character ("L  e  v  i  a  t  h  a  n    (  0  )").
internal static class PortalNameTextTests {
    public static void Run() {
        PortalNamesDefaultWhite();
        PlayerNamesDefaultGold();
        NameFontIsBold();
        NameSizeMatchesFlash();
        LayoutHasNoExtraTracking();
        GlyphQuadsUseFullSize();
    }

    // Non-player floating names (portal "Realm (count)", enemy ShowName)
    // render white; the gold default matches Player.NAME_COLOUR instead.
    private static void PortalNamesDefaultWhite() {
        Equal(Color.White, TypeName.DefaultColorFor(new Entity()));
    }

    private static void PlayerNamesDefaultGold() {
        Equal(new Color(0xFC, 0xDF, 0, 1), TypeName.DefaultColorFor(new Player()));
    }

    // Flash generateNameText calls setBold(true).
    private static void NameFontIsBold() {
        Equal(FontType.Bold, TypeName.NameFont);
    }

    // Flash generateNameText uses a 16px em and both clients map one tile
    // to 50px, so the world scale is 16/50 tiles.
    private static void NameSizeMatchesFlash() {
        Equal(16f / 50f, TypeName.WorldTextSize, 1e-6f);
    }

    // Advance is glyph advance plus a single kerning, like UiLib SimpleText:
    // no per-character bonus (the old 12/PixelRange term put ~19px gaps
    // between glyphs) and no doubled kerning.
    private static void LayoutHasNoExtraTracking() {
        var font = TestFont(new Dictionary<(char, char), float> { [('A', 'B')] = -0.05f });
        var size = TypeName.WorldTextSize;

        var glyphs = TypeName.Layout("AB", font, size, out _, out var total);

        // Advances plus a single kerning: 0.5 - 0.05 + 0.45.
        Equal(0.9f * size, total, 1e-6f);

        // Center-to-center spacing is exactly advance + kern adjusted by the
        // glyph side bearings: 0.5 - 0.05 + 0.155 - 0.175.
        Equal(0.43f * size, glyphs[1].Scale.Z - glyphs[0].Scale.Z, 1e-5f);
    }

    // Quad size is the full glyph extent, not halved: the old /2 shrank
    // glyphs to ~6px at zoom 1 while advances stayed full, reading as tiny
    // text with gaps.
    private static void GlyphQuadsUseFullSize() {
        var font = TestFont(new Dictionary<(char, char), float>());
        var size = TypeName.WorldTextSize;

        var glyphs = TypeName.Layout("A", font, size, out var height, out _);

        Equal(font.Ascender * size, height, 1e-6f);
        Equal((0.4f - -0.05f) * size, glyphs[0].Scale.X, 1e-6f);
        Equal((0.7f - -0.1f) * size, glyphs[0].Scale.Y, 1e-6f);
    }

    private static BitmapFont TestFont(Dictionary<(char, char), float> kernings) {
        var glyphs = new Dictionary<char, FontGlyph> {
            ['A'] = new FontGlyph('A', 0.5f,
                new GlyphData { X0 = -0.05f, X1 = 0.4f, Y0 = 0.7f, Y1 = -0.1f },
                new GlyphData { X0 = 0f, X1 = 0.1f, Y0 = 0f, Y1 = 0.1f }),
            ['B'] = new FontGlyph('B', 0.45f,
                new GlyphData { X0 = -0.04f, X1 = 0.35f, Y0 = 0.68f, Y1 = -0.12f },
                new GlyphData { X0 = 0.1f, X1 = 0.2f, Y0 = 0f, Y1 = 0.1f }),
        };
        return new BitmapFont(new FontData(1.2f, 0.75f, -0.25f, glyphs, kernings), 16f);
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
