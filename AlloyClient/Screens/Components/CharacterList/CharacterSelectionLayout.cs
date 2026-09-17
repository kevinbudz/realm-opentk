using System;

namespace AlloyClient.Screens.Components.CharacterList;

public static class CharacterSelectionLayout
{
    public const int RowWidth = 356;
    public const int RowHeight = 59;
    public const int RowStride = 63;
    public const int ListX = 10;
    public const int ListY = 112;
    public const int ViewHeight = 400;
    public const int NewsX = 400;
    public const int NewsWidth = 388;
    public const int NewsHeight = 52;
    public const int SlotPrice = 2000;
    // Flash CharacterSelectionAndNewsScreen divider line at y=105.
    public const int DividerY = 105;
    // Flash CurrentCharacterRect/CreateNewCharacterRect tagline icon/text y.
    public const int QuestIconY = 26;
    public const int QuestTextY = 27;
    // News icons render the raw tile (plus 1px atlas padding) stretched to
    // the box. Flash redraws 8px tiles 4x and the 16px oryx tile 2x, so both
    // glyphs are 32px: 32 * 10 / 8 = 40 for 8px tiles, 32 * 18 / 16 = 36.
    public const int NewsIconSize = 40;
    public const int NewsOryxIconSize = 36;
    // CreditDisplay redraws 8px tiles 2x (16px glyphs): 16 * 10 / 8 = 20.
    // Flash boxes carry 12px margins with an 8px text overlap (4px visual
    // gap); these boxes carry ~2px margins, so texts end 2px before the box
    // for the same 4px gap, with a 10px gap between the fame/coin pairs.
    public const int CurrencyIconSize = 20;
    public const int CurrencyTextGap = 2;
    public const int CurrencyPairGap = 10;
    // Currency display is right-aligned to the window edge. Previously at the
    // edge (inset 0) and y=20; nudged left 2px and down 4px.
    public const int CurrencyRightInset = 4;
    public const int CurrencyTopY = 24;

    public static (int X, int Y) CurrencyPosition(int windowWidth, float scaleX, float scaleY) =>
        (windowWidth - (int)MathF.Round(CurrencyRightInset * scaleX),
            (int)MathF.Round(CurrencyTopY * scaleY));

    public static int RowY(int index) => 4 + index * RowStride;
    public static int ListHeight(int characters, int availableSlots) => (characters + availableSlots + 1) * RowStride;
    public static int NewsY(int index) => 4 + index * (NewsHeight + 4);

    public static string Ordinal(int number)
    {
        var suffix = number % 100 is >= 11 and <= 13 ? "th" : (number % 10) switch
        {
            1 => "st",
            2 => "nd",
            3 => "rd",
            _ => "th"
        };
        return $"{number}{suffix}";
    }
}
