using System;
using System.Collections.Generic;
using Alloy.UiLib.BuiltIn;
using Alloy.UiLib.Core;
using AlloyClient.Game.Components.Options.Ui;
using OpenTK.Mathematics;

namespace AlloyClient.Game.Components.Options.OptionTypes;

// A standard On/Off toggle with a slider bar anchored to the right of the
// same row. While the master toggle is off the slider is disabled and the
// slider value is ignored (everything renders fully opaque).
public class ToggleSliderOption : Option
{
    private readonly static string[] MasterLabels = ["On", "Off"];
    private readonly static object[] MasterValues = [true, false];

    private const int SliderHeight = 32;
    private const int TrackWidth = 220;
    private const int TrackHeight = 6;
    private const int HandleWidth = 10;
    private const int HandleHeight = 22;
    private const int PercentWidth = 52;

    // Same content width as ToggleGroupOption's sub-checkbox row.
    private const int RowWidth = OptionsView.PanelWidth - 80;
    private const int PercentX = RowWidth - PercentWidth;
    private const int TrackX = PercentX - 12 - TrackWidth;

    public override int RowSpan => 1;

    public override bool PinLeft => true;

    private readonly ValueSetting<bool> _master;
    private readonly ValueSetting<float> _slider;
    private readonly ChoiceBox<bool> _masterBox;
    private readonly ColorRect _fill;
    private readonly ColorRect _handle;
    private readonly SimpleText _percent;
    private readonly List<Sprite> _sliderVisuals = [];
    private readonly Action _masterCallback;
    private readonly Action<float> _sliderCallback;
    private bool _dragging;
    private bool _sliderDisabled;

    internal bool SliderEnabled => !_sliderDisabled;

    public ToggleSliderOption(ValueSetting<bool> master, ValueSetting<float> slider,
        string desc, string tooltipDesc, Action masterCallback = null, Action<float> sliderCallback = null)
        : base(master, desc, tooltipDesc)
    {
        _master = master;
        _slider = slider;
        _masterCallback = masterCallback;
        _sliderCallback = sliderCallback;

        _masterBox = new ChoiceBox<bool>(master, MasterLabels, MasterValues, OnMasterChange);
        AddChild(_masterBox);

        SetHitboxType(CollisionType.Custom);

        var sliderY = (KeyCodeBox.BoxHeight - SliderHeight) / 2;

        var track = new ColorRect(new ColorRectConfig
        {
            X = TrackX,
            Y = sliderY + SliderHeight / 2 - TrackHeight / 2,
            Width = TrackWidth,
            Height = TrackHeight,
            Color = 0x5A5A5A
        });
        AddChild(track);
        _sliderVisuals.Add(track);

        _fill = new ColorRect(new ColorRectConfig
        {
            X = TrackX,
            Y = sliderY + SliderHeight / 2 - TrackHeight / 2,
            Width = 1,
            Height = TrackHeight,
            Color = 0xB3B3B3
        });
        AddChild(_fill);
        _sliderVisuals.Add(_fill);

        _handle = new ColorRect(new ColorRectConfig
        {
            Y = sliderY + SliderHeight / 2 - HandleHeight / 2,
            Width = HandleWidth,
            Height = HandleHeight,
            Color = 0xFFFFFF
        });
        AddChild(_handle);
        _sliderVisuals.Add(_handle);

        _percent = new SimpleText(new TextConfig
        {
            Text = "100%",
            FontSize = 14,
            FontType = FontType.Bold,
            OutlineThickness = 2,
            Color = 0xFFFFFF,
            X = PercentX,
            Y = sliderY + (SliderHeight - 14) / 2 - 2
        });
        AddChild(_percent);
        _sliderVisuals.Add(_percent);

        MouseEnabled = true;
        AddEventListener(MouseEvent.LeftDown, OnLeftDown);
        AddEventListener(MouseEvent.MouseMove, OnMouseMove);
        AddEventListener(MouseEvent.LeftUp, OnLeftUp);
        AddEventListener(MouseEvent.MouseOver, OnMouseOver);
        AddEventListener(MouseEvent.MouseOut, OnMouseOut);

        Refresh();
    }

    protected override bool CustomHitbox(Vector2i pos)
    {
        if (_sliderDisabled)
        {
            return false;
        }

        return pos.X >= TrackX && pos.X <= PercentX + PercentWidth &&
               pos.Y >= 0 && pos.Y <= KeyCodeBox.BoxHeight;
    }

    public override void Refresh()
    {
        _masterBox.Refresh();
        SetVisualValue(_slider.Value);
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
        _sliderDisabled = _disabled || !_master.Value;
        foreach (var visual in _sliderVisuals)
        {
            visual.Alpha = _sliderDisabled ? 0.45f : 1f;
        }
    }

    private void OnLeftDown(MouseEvent args)
    {
        if (_sliderDisabled)
        {
            return;
        }

        _dragging = true;
        CapturePointer();
        SetFromMouse(args.Coords);
    }

    private void OnMouseMove(MouseEvent args)
    {
        if (!_dragging)
        {
            return;
        }

        SetFromMouse(args.Coords);
    }

    private void OnLeftUp(MouseEvent args)
    {
        if (!_dragging)
        {
            return;
        }

        SetFromMouse(args.Coords);
        _dragging = false;
        ReleasePointer();
        Settings.SaveSettings();
    }

    private void SetFromMouse(Vector2 coords)
    {
        var local = GlobalToLocal(coords);
        var value = Math.Clamp((local.X - TrackX) / TrackWidth, 0f, 1f);
        _slider.Set(value);
        SetVisualValue(value);
        _sliderCallback?.Invoke(value);
    }

    private void SetVisualValue(float value)
    {
        var clamped = Math.Clamp(value, 0f, 1f);
        var filledWidth = Math.Max(1, (int)MathF.Round(TrackWidth * clamped));
        _fill.Width = filledWidth;
        _handle.X = TrackX + filledWidth - _handle.Width / 2;
        _percent.SetText($"{(int)MathF.Round(clamped * 100)}%");
    }

    private void OnMouseOver()
    {
        if (!_sliderDisabled)
        {
            _fill.SetColor(0xFFFFFF);
        }
    }

    private void OnMouseOut()
    {
        if (!_sliderDisabled)
        {
            _fill.SetColor(0xB3B3B3);
        }
    }
}
