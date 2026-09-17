using AlloyClient.Ui.Components.Buttons;
using AlloyClient.Utils;
using Alloy.UiLib.Core;
using Alloy.UiLib.Data;

namespace AlloyClient.Ui.Components.Elements;

public class SoundIcon : Sprite {

    private readonly TextureInfo _soundOn;
    private readonly TextureInfo _soundOff;
    private readonly IconButton _button;

    public SoundIcon() {
        _soundOn = TextureHelper.FromGameAtlas("lofiInterfaceBig", 3);
        _soundOff = TextureHelper.FromGameAtlas("lofiInterfaceBig", 4);

        _button = new IconButton(new IconButtonConfig {
            Texture = Settings.PlaySfx.Value ? _soundOn : _soundOff,
            X = -2,
            Y = -2,
            Width = 36,
            Height = 36,
            GameObjectShade = false,
            OnClick = OnIconClick
        });
        AddChild(_button);
    }

    private void OnIconClick() {
        var value = !Settings.PlaySfx.Value;
        Settings.PlaySfx.Set(value);
        Settings.PlayPewPew.Set(value);
        Settings.SaveSettings();
        Audio.SfxChannel.SetVolume(Settings.GetSfxVolume());
        _button.ChangeTexture(value ? _soundOn : _soundOff);
    }
}
