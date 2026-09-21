using System;
using Alloy.UiLib.BuiltIn;
using Alloy.UiLib.Core;
using OpenTK.Mathematics;

namespace AlloyClient.Game.Components.Options.Ui;

// Flash parity: CheckBoxField (account UI) draws a 20px dark box with a
// light-gray X when checked and a bold label beside it.
public class CheckBox : Sprite {
    public const int BoxSize = 20;

    private readonly ValueSetting<bool> _setting;
    private readonly Action _callback;

    private readonly ColorRect _background;
    private readonly Sprite _check;
    private bool _disabled;

    public CheckBox(ValueSetting<bool> setting, string label, Action callback) {
        MouseEnabled = true;

        _setting = setting;
        _callback = callback;

        var outline = new ColorRect(new ColorRectConfig {
            Width = BoxSize,
            Height = BoxSize,
            Color = 0x454545
        });
        AddChild(outline);

        _background = new ColorRect(new ColorRectConfig {
            X = 2,
            Y = 2,
            Width = BoxSize - 4,
            Height = BoxSize - 4,
            Color = 0x333333
        });
        AddChild(_background);

        _check = new Sprite();
        _check.MouseEnabled = false;
        var armLength = (int) MathF.Round((BoxSize - 4) * MathF.Sqrt(2));
        foreach (var rotation in new[] { MathF.PI / 4, -MathF.PI / 4 }) {
            var arm = new ColorRect(new ColorRectConfig {
                Width = armLength,
                Height = 4,
                Color = 0xB3B3B3,
                Anchor = UiAnchor.Middle
            });
            arm.MouseEnabled = false;
            arm.X = BoxSize / 2;
            arm.Y = BoxSize / 2;
            arm.Rotation = rotation;
            _check.AddChild(arm);
        }
        AddChild(_check);

        var text = new SimpleText(new TextConfig {
            Text = label,
            FontSize = 16,
            FontType = FontType.Bold,
            OutlineThickness = 2,
            Color = 0xB3B3B3,
            X = BoxSize + 8,
            Y = (BoxSize - 16) / 2 - 2
        });
        AddChild(text);

        AddEventListener(MouseEvent.MouseOver, () => _background.SetColor(0x555555));
        AddEventListener(MouseEvent.MouseOut, () => _background.SetColor(0x333333));
        AddEventListener(MouseEvent.LeftClick, OnClick);

        Refresh();
    }

    private void OnClick() {
        if (_disabled) {
            return;
        }

        _setting?.Set(!_setting.Value);
        Refresh();
        _callback?.Invoke();
        Settings.SaveSettings();
    }

    public void Refresh() {
        if (_setting is null) {
            return;
        }

        _check.Visible = _setting.Value;
    }

    public void SetDisabled(bool disabled) {
        _disabled = disabled;
        MouseEnabled = !disabled;
        Alpha = disabled ? 0.45f : 1f;
        _background.SetColor(disabled ? 0x303030u : 0x333333u);
    }
}
