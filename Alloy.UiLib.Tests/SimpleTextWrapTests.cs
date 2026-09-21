using Alloy.Common.Structs;
using Alloy.ContentReader;
using Alloy.UiLib.BuiltIn;
using Alloy.UiLib.Core;
using Alloy.UiLib.Data;

namespace Alloy.UiLib.Tests;

// SellableObjectPanel names lay out in a WIDTH-44 field (MaxWidth 144 at
// 16px bold). "Amulet of Resurrection" wrapped to two lines while "Abyss of
// Demons Key" - only wider than the field once its final character is
// added - stayed on one line and rendered past the panel edge. Overflow on
// the final character must still wrap at the last space.
internal static class SimpleTextWrapTests {
    private const int MaxWidth = 144;
    private const float FontSize = 16;

    // Fake advances: 0.625 * 16 = 10px per glyph, no kerning.
    private const float LineHeight = 1.2f;

    public static void Run() {
        TerminalOverflowWrapsAtLastSpace();
        MidStringOverflowStillWraps();
        FittingTextStaysSingleLine();
        TrailingSpaceCreatesNoEmptyLine();
    }

    // The reported shape: 15 glyphs = 150px; the first 14 fit in 144, so
    // the old `i < len - 1` gate never fired and the line ran 6px past its
    // bounds. Now the last word moves down and the block fits the field.
    private static void TerminalOverflowWrapsAtLastSpace() {
        var text = new SimpleText(new TextConfig { Text = "AAAAAA AAAAAAAB", FontSize = FontSize, MaxWidth = MaxWidth },
            TestFont());
        Equal(2, Lines(text));
        if (text.Width > MaxWidth)
            throw new Exception($"expected width <= {MaxWidth}, got {text.Width}");
    }

    // Overflow mid-string (the "Amulet" shape) keeps working as before.
    private static void MidStringOverflowStillWraps() {
        var text = new SimpleText(new TextConfig { Text = "AAAAAA AAAAAAAAAAAAAA", FontSize = FontSize, MaxWidth = MaxWidth },
            TestFont());
        Equal(2, Lines(text));
        if (text.Width > MaxWidth)
            throw new Exception($"expected width <= {MaxWidth}, got {text.Width}");
    }

    // Text that fits (13 glyphs = 130px) stays on one line.
    private static void FittingTextStaysSingleLine() {
        var text = new SimpleText(new TextConfig { Text = "AAAAAA AAAAAA", FontSize = FontSize, MaxWidth = MaxWidth },
            TestFont());
        Equal(1, Lines(text));
        Equal(130, text.Width);
    }

    // A trailing space pushing past the limit must not open an empty line.
    private static void TrailingSpaceCreatesNoEmptyLine() {
        var text = new SimpleText(new TextConfig { Text = "AAAAAA AAAAAA ", FontSize = FontSize, MaxWidth = MaxWidth },
            TestFont());
        Equal(1, Lines(text));
    }

    private static int Lines(SimpleText text) {
        return (int)MathF.Round((float)text.Height / (LineHeight * FontSize));
    }

    private static BitmapFont TestFont() {
        var blank = new GlyphData();
        var glyphs = new Dictionary<char, FontGlyph> {
            ['A'] = new FontGlyph('A', 0.625f, blank, blank),
            ['B'] = new FontGlyph('B', 0.625f, blank, blank),
            [' '] = new FontGlyph(' ', 0.625f, blank, blank),
        };
        return new BitmapFont(new FontData(LineHeight, 0.75f, -0.25f, glyphs, new Dictionary<(char, char), float>()), 16f);
    }

    private static void Equal<T>(T expected, T actual) {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new Exception($"expected {expected}, got {actual}");
    }
}
