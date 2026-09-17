using System;
using Alloy.UiLib.BuiltIn;
using Alloy.UiLib.Core;
using AlloyClient.Ui;
using AlloyClient.Ui.Flash;
using AlloyClient.Utils;

namespace AlloyClient.Ui.Components.Elements;

public class RankText : Sprite
{

    public Container StarSprite;
    public bool LargeText;

    private int _numStars = -1;
    private readonly SimpleText _prefix;

    public RankText(int numStars, bool largeText, bool includePrefix)
    {
        LargeText = largeText;
        if (includePrefix)
        {
            _prefix = new SimpleText(new TextConfig
            {
                Text = "Rank: ",
                FontSize = LargeText ? 18 : 16,
                FontType = LargeText ? FontType.Bold : FontType.Normal,
                Color = FlashUiTokens.TextSecondary,
                DropShadow = FlashTextFilters.Default
            });
            AddChild(_prefix);
        }

        MouseEnabled = false;
        Draw(numStars);
    }

    public void Draw(int numStars)
    {
        if (numStars == _numStars)
        {
            return;
        }

        _numStars = numStars;
        if (StarSprite != null && Contains(StarSprite))
        {
            RemoveChild(StarSprite);
        }

        if (_numStars < 0)
        {
            return;
        }

        StarSprite = new Container();

        var text = new SimpleText(new TextConfig
        {
            Text = _numStars.ToString(),
            FontSize = LargeText ? 18 : 16,
            FontType = LargeText ? FontType.Bold : FontType.Normal,
            Color = FlashUiTokens.TextSecondary,
            DropShadow = FlashTextFilters.StrongOutline
        });

        var iconSize = LargeText ? 16.8f : 12f;
        var icon = new ObjectRect(new ObjectRectConfig
        {
            Texture = TextureHelper.FromUiAtlas("CharacterList/StarGraphic", 0, false),
            Width = LargeText ? 17 : 12,
            Height = LargeText ? 17 : 12,
            OutlineEnabled = false,
            GlowEnabled = false
        });
        icon.ColorTransformation = FameUtils.StarsToColor(_numStars);
        icon.X = text.Width + 5;
        icon.Y = (int)(text.Height / 2f - iconSize / 2f) + 1;
        text.X = 1;
        text.Y = 4;

        var badge = new RoundedRect(new RoundedRectConfig
        {
            X = -2,
            Y = icon.Y - 3,
            Width = (int)MathF.Round(icon.X + iconSize + 6),
            Height = (int)MathF.Round(iconSize + 8),
            Radius = 6,
            Color = 0x000000,
            Alpha = 0.4f
        });

        StarSprite.AddChild(badge);
        StarSprite.AddChild(text);
        StarSprite.AddChild(icon);
        AddChild(StarSprite);

        if (_prefix != null)
        {
            AddChild(_prefix);
            StarSprite.X = _prefix.Width;
        }
    }
}
