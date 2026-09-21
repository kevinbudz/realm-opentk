using System;
using Alloy.UiLib.BuiltIn;
using Alloy.UiLib.Core;
using OpenTK.Mathematics;

namespace AlloyClient.Game.Components.Options.OptionTypes;

public class SliderOption : Option {
    private const int SliderHeight = 32;
    private const int BarX = 170;
    private const int BarWidth = 300;
    private const int TrackX = BarX + 8;
    private const int TrackWidth = BarWidth - 16;
    private const int TrackHeight = 6;
    private const int HandleWidth = 10;
    private const int HandleHeight = 22;
    private const int PercentX = BarX + BarWidth + 16;

    private readonly ValueSetting<float> _setting;
    private readonly Action<float> _sliderCallback;
    private readonly ColorRect _fill;
    private readonly ColorRect _handle;
    private readonly SimpleText _percent;
    private bool _dragging;

    public SliderOption(ValueSetting<float> setting, string text, Action<float> sliderCallback) : base(setting, null, null) {
        _setting = setting;
        _sliderCallback = sliderCallback;

        SetHitboxType(CollisionType.Custom);

        var title = new SimpleText(new TextConfig {
            Text = text,
            FontSize = 18,
            OutlineThickness = 2,
            Color = 0xB3B3B3,
            Y = (SliderHeight - 18) / 2 - 2
        });
        AddChild(title);

        var track = new ColorRect(new ColorRectConfig {
            X = TrackX,
            Y = SliderHeight / 2 - TrackHeight / 2,
            Width = TrackWidth,
            Height = TrackHeight,
            Color = 0x5A5A5A
        });
        AddChild(track);

        _fill = new ColorRect(new ColorRectConfig {
            X = TrackX,
            Y = SliderHeight / 2 - TrackHeight / 2,
            Width = 1,
            Height = TrackHeight,
            Color = 0xB3B3B3
        });
        AddChild(_fill);

        _handle = new ColorRect(new ColorRectConfig {
            Y = SliderHeight / 2 - HandleHeight / 2,
            Width = HandleWidth,
            Height = HandleHeight,
            Color = 0xFFFFFF
        });
        AddChild(_handle);

        _percent = new SimpleText(new TextConfig {
            Text = "100%",
            FontSize = 14,
            FontType = FontType.Bold,
            OutlineThickness = 2,
            Color = 0xFFFFFF,
            X = PercentX,
            Y = (SliderHeight - 14) / 2 - 2
        });
        AddChild(_percent);

        MouseEnabled = true;
        AddEventListener(MouseEvent.LeftDown, OnLeftDown);
        AddEventListener(MouseEvent.MouseMove, OnMouseMove);
        AddEventListener(MouseEvent.LeftUp, OnLeftUp);
        AddEventListener(MouseEvent.MouseOver, OnMouseOver);
        AddEventListener(MouseEvent.MouseOut, OnMouseOut);

        Refresh();
    }

    protected override bool CustomHitbox(Vector2i pos) {
        return pos.X >= BarX && pos.X <= BarX + BarWidth && pos.Y >= 0 && pos.Y <= SliderHeight;
    }

    public override void Refresh() {
        SetVisualValue(_setting.Value);
    }

    public override void SetDisabled(bool val) {
        _disabled = val;
        MouseEnabled = !val;
        Alpha = val ? 0.45f : 1f;
    }

    private void OnLeftDown(MouseEvent args) {
        if (_disabled) {
            return;
        }

        _dragging = true;
        CapturePointer();
        SetFromMouse(args.Coords);
    }

    private void OnMouseMove(MouseEvent args) {
        if (!_dragging) {
            return;
        }

        SetFromMouse(args.Coords);
    }

    private void OnLeftUp(MouseEvent args) {
        if (!_dragging) {
            return;
        }

        SetFromMouse(args.Coords);
        _dragging = false;
        ReleasePointer();
        Settings.SaveSettings();
    }

    private void SetFromMouse(Vector2 coords) {
        var local = GlobalToLocal(coords);
        var value = Math.Clamp((local.X - TrackX) / TrackWidth, 0f, 1f);
        _setting.Set(value);
        SetVisualValue(value);
        _sliderCallback?.Invoke(value);
    }

    private void SetVisualValue(float value) {
        var clamped = Math.Clamp(value, 0f, 1f);
        var filledWidth = Math.Max(1, (int)MathF.Round(TrackWidth * clamped));
        _fill.Width = filledWidth;
        _handle.X = TrackX + filledWidth - _handle.Width / 2;
        _percent.SetText($"{(int)MathF.Round(clamped * 100)}%");
    }

    private void OnMouseOver() {
        if (!_disabled) {
            _fill.SetColor(0xFFFFFF);
        }
    }

    private void OnMouseOut() {
        if (!_disabled) {
            _fill.SetColor(0xB3B3B3);
        }
    }
}
