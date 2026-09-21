using AlloyClient.Game.Components.Options.OptionTypes;

namespace Alloy.UiLib.Tests;

// Section dividers are visual only: no setting, full-width rule geometry,
// and inert refresh/disabled behavior.
internal static class DividerTests {
    public static void Run() {
        FullWidthRuleGeometry();
    }

    private static void FullWidthRuleGeometry() {
        var divider = new DividerOption();
        Equal(true, divider.FullWidth);
        Equal(DividerOption.DividerHeight, divider.RowHeight);
        Equal(null, divider.Setting);
        Equal(1, divider.NumChildren);

        var rule = divider.GetChildAt(0);
        Equal(760, rule?.Width);
        Equal((18 - 2) / 2, rule?.Y);

        divider.Refresh();
        divider.SetDisabled(true);
    }

    private static void Equal<T>(T expected, T? actual) {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new Exception($"expected {expected}, got {actual}");
    }
}
