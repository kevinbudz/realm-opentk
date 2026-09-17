using Alloy.UiLib.BuiltIn;
using AlloyClient.Data;
using AlloyClient.Ui.Flash;
using AlloyClient.Utils;

namespace AlloyClient.Screens.Components.CharacterList;

public sealed class CurrencyDisplay : Container
{
    public void SetAccount(AccountData account)
    {
        SetValues(account?.Stats.Credits ?? 0, account?.Stats.Fame ?? 0);
    }

    public void SetValues(int credits, int fame)
    {
        RemoveChildren();
        var size = CharacterSelectionLayout.CurrencyIconSize;
        var coin = new ObjectRect(new ObjectRectConfig
        {
            Texture = TextureHelper.FromGameAtlas("lofiObj3", 0xE1),
            X = -size,
            Width = size,
            Height = size,
            GameObjectShade = false
        });
        var gold = SelectionGraphics.Text(credits.ToString(), 18, 0, 0, 0xFFFFFF);
        gold.DropShadow = FlashTextFilters.StrongOutline;
        var fameText = SelectionGraphics.Text(fame.ToString(), 18, 0, 0, 0xFFFFFF);
        fameText.DropShadow = FlashTextFilters.StrongOutline;
        var (goldX, goldY, fameIconX, fameX) = ComputePositions(gold.Width, gold.Height, fameText.Width);
        gold.X = goldX;
        gold.Y = goldY + 3;
        var fameIcon = new ObjectRect(new ObjectRectConfig
        {
            Texture = TextureHelper.FromGameAtlas("lofiObj3", 0xE0),
            X = fameIconX,
            Width = size,
            Height = size,
            GameObjectShade = false
        });
        fameText.X = fameX;
        fameText.Y = goldY + 3;
        AddChild(coin);
        AddChild(gold);
        AddChild(fameIcon);
        AddChild(fameText);
    }

    public static (int GoldX, int GoldY, int FameIconX, int FameX) ComputePositions(int goldWidth, int goldHeight, int fameWidth)
    {
        var size = CharacterSelectionLayout.CurrencyIconSize;
        var coinX = -size;
        var goldX = coinX - CharacterSelectionLayout.CurrencyTextGap - goldWidth;
        var goldY = (size - goldHeight) / 2;
        var fameIconX = goldX - CharacterSelectionLayout.CurrencyPairGap - size;
        var fameX = fameIconX - CharacterSelectionLayout.CurrencyTextGap - fameWidth;
        return (goldX, goldY, fameIconX, fameX);
    }
}
