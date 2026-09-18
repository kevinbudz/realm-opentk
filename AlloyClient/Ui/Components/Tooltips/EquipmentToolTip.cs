using System;
using System.Collections.Generic;
using AlloyClient.Assets.XmlStructs;
using AlloyClient.Ui.Components.Elements;
using Alloy.UiLib.BuiltIn;
using Alloy.UiLib.Core;
using AlloyClient.Ui.Flash;
using AlloyClient.Utils;

namespace AlloyClient.Ui.Components.Tooltips;

// Full port of the Flash EquipmentToolTip: icon, title, tier tag,
// description, effect list and restriction list. Effect/restriction content
// comes from EquipmentTooltipBuilder (UI-free); this class only lays out
// one SimpleText per color run, since SimpleText renders a single color.
public sealed class EquipmentToolTip : Tooltip
{
    private const int MaxWidth = 230;

    private ItemDesc _itemDesc;

    private ObjectRect Icon;
    private TierText TierTag;
    private SimpleText TitleText;
    private SimpleText DescText;

    private int _y;

    public EquipmentToolTip(ItemDesc itemDesc, int itemData = -1, PlayerTooltipContext player = null,
        string ownerType = TooltipOwnerTypes.CurrentPlayer, IReadOnlyList<string> usableBy = null,
        string specialKeyLabel = null) : base(MaxWidth, 100)
    {
        _itemDesc = itemDesc;
        specialKeyLabel ??= Settings.Special.Key.ToString();
        AddIcon();
        AddTierTag();
        AddTitle(itemData, player);
        AddDescription();
        PositionHeader();
        _y = DescText.Y + DescText.Height + 4;
        var effectsShown = AddEffects(itemData);
        AddRestrictions(itemData, player, ownerType, usableBy, specialKeyLabel, effectsShown);
        DrawSprite();
    }

    private void AddIcon()
    {
        ushort obj = _itemDesc.ObjectType;
        Icon = new ObjectRect(new ObjectRectConfig
        {
            Texture = TextureHelper.FromGameAtlas(obj <= 0 ? (ushort)0x0096 : obj),
            Width = 40,
            Height = 40
        });
        Icon.X = Icon.Y = 5;
        AddChild(Icon);
    }

    private void AddTitle(int itemData, PlayerTooltipContext player)
    {
        var color = EquipmentTooltipBuilder.GetTitleColor(_itemDesc, itemData, player);
        // Tier tag is built first so the title wraps before its measured edge.
        var titleX = EquipmentTooltipLayout.TitleX();
        var titleRight = EquipmentTooltipLayout.TitleRight(TierTag?.X, TierTag?.Width);
        TitleText = new SimpleText(SimpleConfig(_itemDesc.DisplayName, 16, FontType.Bold,
            color, 0x0, 0f, EquipmentTooltipLayout.TitleWidth(titleX, titleRight), FlashTextFilters.Soft));
        TitleText.SetAnchor(UiAnchor.MiddleLeft);
        TitleText.X = titleX;
        AddChild(TitleText);
    }

    private void AddTierTag()
    {
        if (EquipmentTooltipBuilder.GetTierTag(_itemDesc) == null)
        {
            return;
        }

        TierTag = new TierText(_itemDesc);
        TierTag.SetAnchor(UiAnchor.MiddleRight);
        // Right-anchored, so X is the right edge: inset from the tooltip edge.
        TierTag.X = MaxWidth - 24 + (TierTag.Width / 2);
        AddChild(TierTag);
    }

    private void AddDescription()
    {
        DescText = new SimpleText(SimpleConfig(
            EquipmentTooltipBuilder.CollapseWhitespace(_itemDesc.Description), 14, FontType.Normal,
            TooltipPalette.Body, 0x0, 0f, MaxWidth - 12, FlashTextFilters.Soft));
        AddChild(DescText);
    }

    private bool AddEffects(int itemData)
    {
        var effects = EquipmentTooltipBuilder.BuildEffects(_itemDesc, itemData);
        if (effects.Count == 0)
        {
            return false;
        }

        AddSeparator(_y - 2);
        _y += 8;
        foreach (var effect in effects)
        {
            AddEffectRow(effect);
        }
        _y += 8;
        return true;
    }

    private void AddEffectRow(TooltipEffect effect)
    {
        var x = 8;
        var top = _y;
        var rowHeight = 0;

        if (!string.IsNullOrEmpty(effect.Name))
        {
            var name = new SimpleText(SimpleConfig(EquipmentTooltipLayout.EffectLabel(effect.Name), 14, FontType.Normal,
                TooltipPalette.Body, 0x0, 0f, -1, FlashTextFilters.Soft));
            name.X = x;
            name.Y = top;
            AddChild(name);
            x += name.Width;
            rowHeight = Math.Max(rowHeight, name.Height);
        }

        foreach (var segment in effect.Value)
        {
            if (string.IsNullOrEmpty(segment.Text))
            {
                continue;
            }
            var value = new SimpleText(SimpleConfig(segment.Text, 14, FontType.Normal,
                segment.Color, 0x0, 0f, MaxWidth - x - 4, FlashTextFilters.Soft));
            value.X = x;
            value.Y = top;
            AddChild(value);
            x += value.Width;
            rowHeight = Math.Max(rowHeight, value.Height);
        }

        _y = top + rowHeight;
    }

    private void AddRestrictions(int itemData, PlayerTooltipContext player, string ownerType,
        IReadOnlyList<string> usableBy, string specialKeyLabel, bool effectsShown)
    {
        var restrictions = EquipmentTooltipBuilder.BuildRestrictions(
            _itemDesc, itemData, player, ownerType, usableBy, specialKeyLabel);
        if (restrictions.Count == 0 && !effectsShown)
        {
            return;
        }

        AddSeparator(_y - 2);
        _y += 8;
        foreach (var restriction in restrictions)
        {
            var text = new SimpleText(SimpleConfig(restriction.Text, 14,
                restriction.Bold ? FontType.Bold : FontType.Normal,
                restriction.Color, 0x0, 0f, MaxWidth - 12, FlashTextFilters.Soft));
            // Hanging indent mirrors the Flash ".in" rule
            // (margin-left 10, first line outdented).
            text.OffsetLineWrapBy(10);
            text.X = 8;
            text.Y = _y;
            AddChild(text);
            _y += text.Height;
        }
        _y += 8;
    }

    private void AddSeparator(int y)
    {
        AddChild(new ColorRect(new ColorRectConfig
        {
            X = 8,
            Y = y,
            Width = MaxWidth - 16,
            Height = 2,
            Color = 0x1C1C1C
        }));
    }

    private void PositionHeader()
    {
        // Title centers on the icon middle at any line count.
        TitleText.Y = EquipmentTooltipLayout.TitleMiddleY();
        if(TierTag != null)
        {
            // Centered on the icon middle like the title (Flash alignUI).
            TierTag.Y = EquipmentTooltipLayout.TierMiddleY(TitleText.Y);
        }
        DescText.X = 8;
        // Below the icon, but pushed further down when a wrapped title
        // extends past it.
        DescText.Y = EquipmentTooltipLayout.DescY(TitleText.Y, TitleText.Height);
    }

    public override void DrawSprite() {
        ToolHeight = Math.Max(ToolHeight, Height + 10);
        base.DrawSprite();
    }

    public static float Round(float number, int decimalPlaces = 1)
    {
        float exp = MathF.Pow(10, decimalPlaces);
        if (decimalPlaces > 0) {
            number = (int)(number * exp) / exp;
        }
        else if (decimalPlaces == 0) {
            number = (int)number;
        }

        return number;
    }
    public static TextConfig SimpleConfig(string text = "", int size = 12, FontType type = FontType.Normal, uint color = 0xffffff, uint outline = 0x0, float thickness = 1f, int maxWidth = 220, Alloy.UiLib.Extra.DropShadowFilter dropShadow = null)
    {
        return new TextConfig()
        {
            FontSize = size,
            FontType = type,
            Text = text,
            Color = color,
            OutlineColor = outline,
            OutlineThickness = thickness,
            MaxWidth = maxWidth,
            DropShadow = dropShadow,
        };
    }
}
