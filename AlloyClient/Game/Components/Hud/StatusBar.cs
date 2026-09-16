using System;
using AlloyClient.Ui;
using Alloy.UiLib.BuiltIn;
using Alloy.UiLib.Core;
using AlloyClient.Ui.Flash;

namespace AlloyClient.Game.Components.Hud;

public class StatusBar : Sprite {
    private readonly int _width;
    private readonly int _height;

    private NineSliceRect _mainBar;
    private NineSliceRect _backgroundBar;
    private NineSliceRect _outlineBar;
    private SimpleText _label;
    private SimpleText _valueText;

    public string labelString;
    private bool _mouseOver;
    private TextState _textState;

    public StatusBar(int width, int height, uint color, uint backColor, uint outlineColor, string label) {
        _width = width;
        _height = height;

        _backgroundBar = new NineSliceRect(new NineSliceConfig { SliceData = SliceLibrary.StatusBar, CutX = 10, CutY = 10, Width = _width, Height = _height });
        _backgroundBar.SetColor(backColor);
        AddChild(_backgroundBar);

        _mainBar = new NineSliceRect(new NineSliceConfig { SliceData = SliceLibrary.StatusBar, CutX = 10, CutY = 10, Width = _width, Height = _height });
        _mainBar.SetColor(color);
        AddChild(_mainBar);

        labelString = label;
        _label = CreateText(label);
        AddChild(_label);
        
        _valueText = CreateText("");
        AddChild(_valueText);

        AddEventListener(MouseEvent.MouseOver, OnMouseOver);
        AddEventListener(MouseEvent.MouseOut, OnMouseOut);
    }

    public void Update(int val, int max, int boost = 0, int baseMax = -1, int level = -1) {
        var filledWidth = max > 0 ? (int)(_width * (Math.Clamp(val, 0, max) / (float)max)) : _width;
        _mainBar.Resize(filledWidth, _height);
        _valueText.Visible = _mouseOver || Settings.ToggleBarText;
        UpdateText(val, max, boost, baseMax, level);
    }

    public void UpdateLabel(string label)
    {
        if(_label != null)
        {
            RemoveChild(_label);
        }

        labelString = label;
        _label = CreateText(label);
        AddChild(_label);
    }

    private static SimpleText CreateText(string text) => new(new TextConfig {
        Text = text,
        FontSize = 14,
        FontType = FontType.Bold,
        DropShadow = FlashTextFilters.Default,
        X = 1,
        Y = 1,
        Color = 0xFFFFFF
    });

    private void UpdateText(int val, int max, int boost, int baseMax, int level) {
        if (!_valueText.Visible) {
            return;
        }

        var newState = new TextState(val, max, boost, baseMax, level);

        if (newState == _textState) {
            return;
        }

        _textState = newState;
        
        var ltmt = "";

        if (Settings.ToggleLeftToMax) {
            var ltm = baseMax - max - boost;
            if (level >= 20 && ltm > 0)
                ltmt = $"|{Math.Ceiling(ltm / 5f)}";
        }

        if (max > 0)
            _valueText.SetText($"{val}/{max}" + ltmt);
        else
            _valueText.SetText($"{val}");
        // Flash centers valueText_.width (tight + 4px gutter) while Alloy
        // centers tight bounds, so shift left 2px to align glyph centers.
        _valueText.X = _width / 2 - _valueText.Width / 2 - 2;
    }

    private void OnMouseOver() => _mouseOver = true;
    private void OnMouseOut() => _mouseOver = false;

    private record struct TextState(int Value, int Max, int Boost, int BaseMax, int Level);
}
