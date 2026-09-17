using Alloy.Common;
using AlloyClient.Assets.XmlStructs;
using AlloyClient.Ui.Components.Tooltips;
using Alloy.UiLib.BuiltIn;
using Alloy.UiLib.Core;

namespace AlloyClient.Ui.Components.Elements;

public class TierText : Sprite
{
    private SimpleText Tag;
    public TierText(ItemDesc desc)
    {
        // Tier rules live in the tooltip builder so tiles and tooltips agree.
        var tag = EquipmentTooltipBuilder.GetTierTag(desc) ?? ("", 0xFFFFFFu);
        Tag = new SimpleText(new TextConfig()
        {
            FontSize = 16,
            FontType = FontType.Bold,
            Text = tag.Item1,
            OutlineColor = 0,
            OutlineThickness = 2
        });
        Tag.SetColor(tag.Item2);
        AddChild(Tag);
    }
}
