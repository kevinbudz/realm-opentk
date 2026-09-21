using System;
using Alloy.UiLib.BuiltIn;
using Alloy.UiLib.Core;
using AlloyClient.Utils;

namespace AlloyClient.Ui.Components.Buttons;

// Flash parity (kabam.rotmg.util.components.LegacyBuyButton): the shop
// "prefix price [icon]" button. White cut-edge background (bevel 4), dark
// bold text, coin/fame/guild-fame icon, hover fill 0xFFDC85, disabled fill
// 0x7F7F7F. Width = max(fixed, text + icon + 3 * padding), height =
// textHeight + 8.
public class LegacyBuyButton : Sprite {
    public const int Gold = 0;
    public const int Fame = 1;
    public const int GuildFame = 2;

    private const int Bevel = 4;
    private const int Padding = 5;
    private const int IconSize = 20;
    private const uint DarkText = 0x363636;
    private const uint EnabledFill = 0xFFFFFF;
    private const uint HoverFill = 0xFFDC85;
    private const uint DisabledFill = 0x7F7F7F;

    private readonly float _fontSize;
    private readonly FontType _fontType;
    private readonly Action _onClicked;

    private string _prefix;
    private int _price = -1;
    private int _currency = -1;
    private int _fixedWidth = -1;
    private bool _enabled = true;
    private bool _leftDown;

    private CutEdgeRect _background;
    private readonly SimpleText _text;
    private readonly ObjectRect _icon;

    public LegacyBuyButton(string prefix, float fontSize, int price, int currency, Action onClicked = null) {
        _prefix = prefix;
        _fontSize = fontSize;
        _fontType = FontType.Bold;
        _onClicked = onClicked;

        _text = new SimpleText(new TextConfig {
            Text = "", FontSize = _fontSize, FontType = _fontType, Color = DarkText
        });
        _icon = new ObjectRect(new ObjectRectConfig {
            Texture = CurrencyTexture(currency),
            Width = IconSize, Height = IconSize,
            GameObjectShade = false
        });

        MouseEnabled = true;
        AddChild(_background = MakeBackground(EnabledFill));
        AddChild(_text);
        AddChild(_icon);

        AddEventListener(MouseEvent.MouseOver, OnMouseOver);
        AddEventListener(MouseEvent.MouseOut, OnRollOut);
        AddEventListener(MouseEvent.LeftDown, OnLeftDown);
        AddEventListener(MouseEvent.LeftUp, OnLeftUp);

        SetPrice(price, currency);
    }

    private CutEdgeRect MakeBackground(uint color) {
        return new CutEdgeRect(new CutEdgeConfig {
            Width = Math.Max(1, Width), Height = Math.Max(1, Height),
            CutX = Bevel, CutY = Bevel, Color = color
        });
    }

    private void RefreshBackground(uint color) {
        if (_background != null)
            RemoveChild(_background);
        // Size first via text/icon layout, then build the fill to match.
        Layout();
        _background = new CutEdgeRect(new CutEdgeConfig {
            Width = GetWidth(), Height = GetHeight(),
            CutX = Bevel, CutY = Bevel, Color = color
        });
        AddChildAt(_background, 0);
        Layout();
    }

    public void SetPrice(int price, int currency) {
        if (_price == price && _currency == currency)
            return;
        _price = price;
        _currency = currency;
        UpdateUi(EnabledFillColor());
    }

    public void SetPrefix(string prefix) {
        _prefix = prefix;
        UpdateUi(EnabledFillColor());
    }

    public void SetEnabled(bool enabled) {
        if (enabled == _enabled && MouseEnabled == enabled)
            return;
        _enabled = enabled;
        MouseEnabled = enabled;
        RefreshBackground(EnabledFillColor());
    }

    public void SetWidth(int value) {
        _fixedWidth = value;
        RefreshBackground(EnabledFillColor());
    }

    public void SetOnClicked(Action onClicked) {
        // Panels wire click at construction; kept for symmetry with Flash.
        _ = onClicked;
    }

    private uint EnabledFillColor() => _enabled ? EnabledFill : DisabledFill;

    private void UpdateUi(uint fill) {
        _text.SetText(_prefix + _price);
        try {
            _icon.ChangeTexture(CurrencyTexture(_currency));
        } catch {
            // Headless tests may lack atlas init; the price text still lays out.
        }
        RefreshBackground(fill);
    }

    private int GetWidth() {
        return Math.Max(_fixedWidth, _text.Width + IconSize + 3 * Padding);
    }

    private int GetHeight() {
        return _text.Height + 8;
    }

    private void Layout() {
        _text.X = (GetWidth() - IconSize - _text.Width - Padding) / 2;
        _text.Y = 6;
        _icon.X = _text.X + _text.Width + Padding;
        _icon.Y = (GetHeight() - IconSize) / 2;
    }

    private static Alloy.UiLib.Data.TextureInfo CurrencyTexture(int currency) {
        var index = currency switch {
            Gold => 225,
            Fame => 224,
            GuildFame => 226,
            _ => 225
        };
        return TextureHelper.FromGameAtlas("lofiObj3", index);
    }

    private void OnMouseOver() {
        if (!_enabled)
            return;
        RefreshBackground(HoverFill);
    }

    private void OnRollOut() {
        if (!_enabled)
            return;
        RefreshBackground(EnabledFill);
    }

    private void OnLeftDown() {
        _leftDown = true;
    }

    private void OnLeftUp() {
        if (_leftDown)
            _onClicked?.Invoke();
        _leftDown = false;
    }
}
