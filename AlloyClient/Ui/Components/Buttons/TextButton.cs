using System;
using Alloy.UiLib.BuiltIn;
using Alloy.UiLib.Core;
using Alloy.UiLib.Extra;

namespace AlloyClient.Ui.Components.Buttons;

public struct TextButtonConfig {
    public string Text = "";
    public float FontSize = 1f;
    public Action OnClicked = null;
    public FontType FontType = FontType.Bold;
    public uint ActiveColor = 0xFFFFFF;
    public uint HoverColor = 0xFFDC85;
    public uint InactiveColor = 0x363636;
    public int X = 0;
    public int Y = 0;
    public float Alpha = 1.0f;
    public uint OutlineColor = 0x0;
    public float OutlineThickness = 0;
    public DropShadowFilter DropShadow = null;
    public UiAnchor Anchor = UiAnchor.LeftTop;

    // Flash parity (com.company.assembleegameclient.ui.TextButton): when true
    // the button draws the Flash white cut-edge background with dark text
    // (hover 0xFFDC85, disabled 0x7F7F7F) instead of bare text. HUD panels
    // opt in; other screens keep the existing bare-text look.
    public bool FlashBackground = false;
    public int FixedWidth = 0;

    public TextButtonConfig() { }
}

public class TextButton : Sprite {
    // Flash TextButton metrics.
    private const int Bevel = 4;
    private const uint DarkText = 0x363636;
    private const uint EnabledFill = 0xFFFFFF;
    private const uint HoverFill = 0xFFDC85;
    private const uint DisabledFill = 0x7F7F7F;

    private readonly uint _activeColor;
    private readonly uint _onHoverColor;
    private readonly uint _inactive;

    private readonly SimpleText _text;
    private readonly Action _onClicked;
    private readonly bool _flashBackground;
    private readonly int _fixedWidth;
    private CutEdgeRect _background;
    private uint _fillColor = EnabledFill;
    private bool _enabled;

    private bool _leftDown;

    public string Name {
        get => _text.Text;
    }

    public TextButton(TextButtonConfig config) {
        _activeColor = config.ActiveColor;
        _onHoverColor = config.HoverColor;
        _inactive = config.InactiveColor;
        _flashBackground = config.FlashBackground;
        _fixedWidth = config.FixedWidth;

        var textColor = _flashBackground ? DarkText : _activeColor;
        _text = new SimpleText(new TextConfig {Text = config.Text, FontSize = config.FontSize, FontType = config.FontType, Color = textColor, OutlineColor = config.OutlineColor, OutlineThickness = config.OutlineThickness, DropShadow = config.DropShadow});
        _onClicked = config.OnClicked;

        X = config.X;
        Y = config.Y;
        Alpha = config.Alpha;
        SetAnchor(config.Anchor);

        MouseEnabled = true;

        if (_flashBackground) {
            _background = new CutEdgeRect(new CutEdgeConfig {
                Width = ButtonWidth(), Height = ButtonHeight(),
                CutX = Bevel, CutY = Bevel, Color = EnabledFill
            });
            AddChild(_background);
            PositionFlashText();
        }
        AddChild(_text);
        Activate();
    }

    private int ButtonWidth() => Math.Max(_fixedWidth, _text.Width + 12);

    private int ButtonHeight() => _text.Height + 8;

    private void PositionFlashText() {
        _text.X = ButtonWidth() / 2 - _text.Width / 2 - 2;
        _text.Y = 6;
    }

    private void RefreshBackground() {
        if (!_flashBackground || _background == null)
            return;
        _background.Resize(ButtonWidth(), ButtonHeight());
        PositionFlashText();
    }

    public void SetText(string text) {
        _text.SetText(text);
        RefreshBackground();
    }

    public void SetState(bool state) {
        if (state) Activate();
        else Deactivate();
    }

    public void SetEnabled(bool enabled) {
        if (!_flashBackground) {
            SetState(enabled);
            return;
        }
        if (enabled == _enabled)
            return;
        _enabled = enabled;
        MouseEnabled = enabled;
        _fillColor = enabled ? EnabledFill : DisabledFill;
        _background?.Resize(ButtonWidth(), ButtonHeight());
        // Recolor by rebuilding the fill: CutEdgeRect has no recolor, so swap it.
        if (_background != null) {
            RemoveChild(_background);
            _background = new CutEdgeRect(new CutEdgeConfig {
                Width = ButtonWidth(), Height = ButtonHeight(),
                CutX = Bevel, CutY = Bevel, Color = _fillColor
            });
            AddChildAt(_background, 0);
            PositionFlashText();
        }
        if (enabled) {
            AddEventListener(MouseEvent.MouseOver, OnMouseOver);
            AddEventListener(MouseEvent.MouseOut, OnMouseOut);
            AddEventListener(MouseEvent.LeftDown, OnLeftDown);
            AddEventListener(MouseEvent.LeftUp, OnLeftUp);
        } else {
            RemoveEventListener(MouseEvent.MouseOver, OnMouseOver);
            RemoveEventListener(MouseEvent.MouseOut, OnMouseOut);
            RemoveEventListener(MouseEvent.LeftDown, OnLeftDown);
            RemoveEventListener(MouseEvent.LeftUp, OnLeftUp);
        }
    }

    public void Activate() {
        if (_flashBackground) {
            SetEnabled(true);
            return;
        }
        _text.SetColor(_activeColor);
        AddEventListener(MouseEvent.MouseOver, OnMouseOver);
        AddEventListener(MouseEvent.MouseOut, OnMouseOut);
        AddEventListener(MouseEvent.LeftDown, OnLeftDown);
        AddEventListener(MouseEvent.LeftUp, OnLeftUp);
    }

    public void Deactivate() {
        if (_flashBackground) {
            SetEnabled(false);
            return;
        }
        _text.SetColor(_inactive);
        RemoveEventListener(MouseEvent.MouseOver, OnMouseOver);
        RemoveEventListener(MouseEvent.MouseOut, OnMouseOut);
        RemoveEventListener(MouseEvent.LeftDown, OnLeftDown);
        RemoveEventListener(MouseEvent.LeftUp, OnLeftUp);
    }

    private void OnMouseOver() {
        if (_flashBackground) {
            if (!_enabled)
                return;
            RemoveChild(_background);
            _background = new CutEdgeRect(new CutEdgeConfig {
                Width = ButtonWidth(), Height = ButtonHeight(),
                CutX = Bevel, CutY = Bevel, Color = HoverFill
            });
            AddChildAt(_background, 0);
            PositionFlashText();
            return;
        }
        _text.SetColor(_onHoverColor);
    }

    private void OnMouseOut() {
        if (_flashBackground) {
            if (!_enabled)
                return;
            RemoveChild(_background);
            _background = new CutEdgeRect(new CutEdgeConfig {
                Width = ButtonWidth(), Height = ButtonHeight(),
                CutX = Bevel, CutY = Bevel, Color = EnabledFill
            });
            AddChildAt(_background, 0);
            PositionFlashText();
            return;
        }
        _text.SetColor(_activeColor);
    }

    private void OnLeftDown() {
        _leftDown = true;
    }

    private void OnLeftUp() {
        if (_leftDown) {
            _onClicked?.Invoke();
        }

        _leftDown = false;
    }
}
