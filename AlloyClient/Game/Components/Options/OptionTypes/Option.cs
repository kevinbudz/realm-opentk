using AlloyClient.Game.Components.Options.Ui;
using Alloy.UiLib.BuiltIn;
using Alloy.UiLib.Core;

namespace AlloyClient.Game.Components.Options.OptionTypes;

public abstract class Option : Sprite {
    // TODO: tooltip
    public readonly ISettingType Setting;
    protected readonly SimpleText DescText;
    protected bool _disabled;

    // Grid rows this option occupies. Tall options (e.g. toggle groups with
    // a sub-checkbox row) span two rows so neighbours never overlap them.
    public virtual int RowSpan => 1;

    // Tall options stick to the left column so their extra rows never
    // collide with the alternating flow.
    public virtual bool PinLeft => false;

    // Full-width options (e.g. section dividers) take their own row across
    // both columns.
    public virtual bool FullWidth => false;

    // Pixel height this option reserves in its column.
    public virtual int RowHeight => 44 * RowSpan;

    protected Option(ISettingType setting, string desc, string tooltipDesc) {
        Setting = setting;
        
        if (!string.IsNullOrEmpty(desc)) {
            DescText = new SimpleText(new TextConfig {
                Text = desc,
                FontSize = 18,
                OutlineThickness = 2,
                Color = 0xB3B3B3,
                X = KeyCodeBox.BoxWidth + 24,
                Y = (KeyCodeBox.BoxHeight - 18) / 2 - 2,
            });
            AddChild(DescText);
        }

        if (!string.IsNullOrEmpty(tooltipDesc)) {
            // Add tooltip here
        }
    }

    public abstract void Refresh();

    public abstract void SetDisabled(bool val);
}
