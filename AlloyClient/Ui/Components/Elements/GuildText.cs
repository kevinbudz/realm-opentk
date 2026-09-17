using Alloy.UiLib.BuiltIn;
using Alloy.UiLib.Core;
using AlloyClient.Ui;
using AlloyClient.Ui.Flash;

namespace AlloyClient.Ui.Components.Elements;

public class GuildText : Sprite {

    private string _name;
    private int _rank;

    private readonly ObjectRect _icon;
    private readonly SimpleText _guildName;

    public GuildText(string name, int rank, int w = 0) {
        var stWidth = w == 0 ? 0 : w + 16;
        _icon = new ObjectRect(new ObjectRectConfig {
            X = 3,
            Y = 3,
            Width = 18,
            Height = 18,
            OutlineColor = 0x0,
            GameObjectShade = false
        });
        _guildName = new SimpleText(new TextConfig {
            FontSize = 16,
            Color = FlashUiTokens.TextPrimary,
            MaxWidth = stWidth == 0 ? -1 : stWidth,
            X = 24,
            DropShadow = FlashTextFilters.Default
        });
        Draw(name, rank);
    }

    public void Draw(string name, int rank) {
        if (_name == name && rank == _rank) {
            return;
        }

        _name = name;
        _rank = rank;
        if (_name == null || _name == "") {
            RemoveChild(_icon);
            RemoveChild(_guildName);
            return;
        }

        if (GuildUtils.RankToIcon(rank) is not { } texture) {
            _icon.Visible = false;
        } else {
            _icon.Visible = true;
            _icon.ChangeTexture(texture);
        }

        _guildName.SetText(name);
        AddChild(_icon);
        AddChild(_guildName);
    }
}
