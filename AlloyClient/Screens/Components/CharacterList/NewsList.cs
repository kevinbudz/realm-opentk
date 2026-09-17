using System;
using Alloy.UiLib.BuiltIn;
using Alloy.UiLib.Core;
using AlloyClient.Data;
using AlloyClient.Utils;

namespace AlloyClient.Screens.Components.CharacterList;

public sealed class NewsList : Container
{
    public NewsList(NewsData news, Action<NewsItem> open)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var items = news?.NewsList ?? [];
        for (var i = 0; i < items.Length; i++)
        {
            var item = items[i];
            var line = new Container(new ContainerConfig
            {
                Y = CharacterSelectionLayout.NewsY(i),
                Width = CharacterSelectionLayout.NewsWidth,
                Height = CharacterSelectionLayout.NewsHeight
            })
            { MouseEnabled = true };
            var (sheet, index, iconSize) = item.Icon.ToLowerInvariant() switch
            {
                "fame" => ("lofiObj3", 0xE0, CharacterSelectionLayout.NewsIconSize),
                "oryx" => ("lofiChar16x16", 0x54, CharacterSelectionLayout.NewsOryxIconSize),
                _ => ("lofiInterface2", 4, CharacterSelectionLayout.NewsIconSize)
            };
            line.AddChild(new ObjectRect(new ObjectRectConfig
            {
                Texture = TextureHelper.FromGameAtlas(sheet, index),
                X = 10,
                Y = 20 - iconSize / 2,
                Width = iconSize,
                Height = iconSize,
                GameObjectShade = false
            }));
            var title = SelectionGraphics.Text(item.Title, 18, 61, 0);
            var tagline = SelectionGraphics.Text(item.TagLine, 14, 61, 24);
            var date = SelectionGraphics.Text(item.RelativeTime(now), 16, 0, 0);
            date.X = CharacterSelectionLayout.NewsWidth - date.Width;
            line.AddChild(title);
            line.AddChild(tagline);
            line.AddChild(date);
            line.AddEventListener(MouseEvent.MouseOver, () => SetColor(0xFFC800));
            line.AddEventListener(MouseEvent.MouseOut, () => SetColor(0xB3B3B3));
            line.AddEventListener(MouseEvent.LeftDown, () => open(item));
            AddChild(line);

            void SetColor(uint color)
            {
                title.SetColor(color);
                tagline.SetColor(color);
                date.SetColor(color);
            }
        }
    }
}
