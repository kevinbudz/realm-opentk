using Alloy.UiLib.BuiltIn;

namespace AlloyClient.Game.Components.Options.OptionTypes;

// A full-width horizontal rule separating sections of an options tab.
// Visual only: it carries no setting and ignores refresh/disabled.
public class DividerOption : Option
{
    public const int DividerHeight = 18;
    private const int RuleHeight = 2;

    public override bool FullWidth => true;

    public override int RowHeight => DividerHeight;

    public DividerOption() : base(null, null, null)
    {
        AddChild(new ColorRect(new ColorRectConfig
        {
            Width = OptionsView.PanelWidth - 70,
            Y = (DividerHeight - RuleHeight) / 2 - 6,
            Height = RuleHeight,
            Color = 0x5E5E5E
        }));
    }

    public override void Refresh()
    {
    }

    public override void SetDisabled(bool val)
    {
    }
}
