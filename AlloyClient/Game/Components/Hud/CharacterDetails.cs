using AlloyClient.Game.Objects;
using Alloy.UiLib.BuiltIn;
using Alloy.UiLib.Core;
using AlloyClient.Utils;
using AlloyClient.Display;
using AlloyClient.Game.Components;
using AlloyClient.Game.Components.Options;
using AlloyClient.Ui.Components.Buttons;
using AlloyClient.Ui.Flash;

namespace AlloyClient.Game.Components.Hud;

public sealed class CharacterDetails : Sprite {

    private readonly ObjectRect _skin;
    private readonly SimpleText _name;

    public CharacterDetails() {
        // Flash getPortrait() renders a 20px visual inside a 44px bitmap at
        // (-2,-8), so the visual sits at (10,4) with room for the black
        // outline glow. Use a padded 25px quad (20px visual + 2.5px border)
        // at (8,2) so the outline has room instead of clipping.
        _skin = new ObjectRect(new ObjectRectConfig { Width = 25, Height = 25 });
        _skin.X = 8;
        _skin.Y = 2;
        AddChild(_skin);
        _name = new SimpleText(new TextConfig {
            Text = "test",
            FontSize = 20,
            FontType = FontType.Bold,
            // Y=3 confirmed against Flash reference slice.
            X = 37,
            Y = 5,
            Color = 0xB3B3B3,
            DropShadow = FlashTextFilters.Default
        });
        AddChild(_name);

        // Flash CharacterDetailsView places a 16px lofiInterfaceBig visual at
        // (172,4) via a 40px IconButton bitmap at (-12,-12) with a black
        // outline. Use a padded 18px quad so the 16px visual keeps 1px for
        // the outline glow.
        var options = new ObjectRect(new ObjectRectConfig {
            Texture = TextureHelper.FromGameAtlas("lofiInterfaceBig", 5),
            X = 171,
            Y = 3,
            Width = 18,
            Height = 18,
            MouseEnabled = true
        });
        options.AddEventListener(MouseEvent.LeftUp, () => {
            UserInput.SetManualFocus(false);
            OverlayManager.Set(new OptionsView());
        });
        AddChild(options);

        Map.OnPlayerUpdate.Add(OnPlayerUpdate);
    }

    private void OnPlayerUpdate(Player player) {
        _name.SetText(player.Name);
        _skin.ChangeTexture(TextureHelper.Create(player.TextureData.AnimatedTextures.FaceRight[0], TextureType.GameAtlas));
    }
}
