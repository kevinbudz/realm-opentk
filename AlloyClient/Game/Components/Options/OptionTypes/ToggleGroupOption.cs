using System;
using System.Collections.Generic;
using AlloyClient.Game.Components.Options.Ui;

namespace AlloyClient.Game.Components.Options.OptionTypes;

// A standard On/Off toggle with sub-checkboxes anchored to the right of
// the same row. While the master toggle is off the checkboxes are
// disabled.
public class ToggleGroupOption : Option
{
    private readonly static string[] MasterLabels = ["On", "Off"];
    private readonly static object[] MasterValues = [true, false];

    public override int RowSpan => 1;

    public override bool PinLeft => true;

    private readonly ValueSetting<bool> _master;
    private readonly ChoiceBox<bool> _masterBox;
    private readonly List<CheckBox> _subBoxes = [];
    private readonly Action _masterCallback;

    internal IReadOnlyList<CheckBox> SubBoxes => _subBoxes;

    public ToggleGroupOption(ValueSetting<bool> master, (string Label, ValueSetting<bool> Setting)[] subs,
        string desc, string tooltipDesc, Action masterCallback = null)
        : base(master, desc, tooltipDesc)
    {
        _master = master;
        _masterCallback = masterCallback;

        _masterBox = new ChoiceBox<bool>(master, MasterLabels, MasterValues, OnMasterChange);
        AddChild(_masterBox);

        // Sub-checkboxes share the master row, anchored to its right margin
        // (same content width as DividerOption) instead of trailing the
        // description.
        var rowWidth = OptionsView.PanelWidth - 80;
        foreach (var (label, setting) in subs)
        {
            var box = new CheckBox(setting, label, null);
            AddChild(box);
            _subBoxes.Add(box);
        }

        var x = rowWidth;
        foreach (var box in _subBoxes)
        {
            x -= box.Width;
        }
        x -= 24 * (_subBoxes.Count - 1);

        foreach (var box in _subBoxes)
        {
            box.X = x;
            box.Y = (KeyCodeBox.BoxHeight - CheckBox.BoxSize) / 2;
            x += box.Width + 24;
        }

        Refresh();
    }

    public override void Refresh()
    {
        _masterBox.Refresh();
        foreach (var box in _subBoxes)
        {
            box.Refresh();
        }

        ApplyMasterState();
    }

    public override void SetDisabled(bool val)
    {
        _disabled = val;
        _masterBox.SetDisabled(val);
        DescText?.SetColor(val ? 0x666666u : 0xB3B3B3u);
        ApplyMasterState();
    }

    private void OnMasterChange()
    {
        ApplyMasterState();
        _masterCallback?.Invoke();
    }

    private void ApplyMasterState()
    {
        var subDisabled = _disabled || !_master.Value;
        foreach (var box in _subBoxes)
        {
            box.SetDisabled(subDisabled);
        }
    }
}
