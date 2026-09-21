using AlloyClient.Ui.Components.Buttons;
using AlloyClient.Utils;
using Alloy.UiLib.Core;
using Alloy.UiLib.Data;

namespace AlloyClient.Ui.Components.Elements;

public class SoundIcon : Sprite {

    private readonly TextureInfo _soundOn;
    private readonly TextureInfo _soundOff;
    private readonly IconButton _button;

    private float _lastVolume = 1f;

    public SoundIcon() {
        _soundOn = TextureHelper.FromGameAtlas("lofiInterfaceBig", 3);
        _soundOff = TextureHelper.FromGameAtlas("lofiInterfaceBig", 4);

        _button = new IconButton(new IconButtonConfig {
            Texture = Settings.SfxVolume.Value > 0f ? _soundOn : _soundOff,
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
        if (Settings.SfxVolume.Value > 0f) {
            _lastVolume = Settings.SfxVolume.Value;
            Settings.SetSfxVolume(0f);
        } else {
            Settings.SetSfxVolume(_lastVolume <= 0f ? 1f : _lastVolume);
        }

        Settings.SaveSettings();
        Audio.SfxChannel.SetVolume(Settings.GetSfxVolume());
        _button.ChangeTexture(Settings.SfxVolume.Value > 0f ? _soundOn : _soundOff);
    }
}
