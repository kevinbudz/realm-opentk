using Alloy.UiLib.Core;
using Alloy.UiLib.Data;
using AlloyClient.Utils;

namespace AlloyClient.Ui.Components.Buttons;

public struct MusicButtonConfig {
    
    public int X = 0;
    public int Y = 0;
    public int Width = 0;
    public int Height = 0;
    public float Alpha = 1.0f;
    public UiAnchor Anchor = UiAnchor.LeftTop;
    
    public MusicButtonConfig() { }
}

public class MusicButton : UiElement {

    private readonly TextureInfo _musicOn;
    private readonly TextureInfo _musicOff;

    private readonly IconButton _button;

    private float _lastVolume = 1f;

    //TODO: turn into its own thing instead of having icon child

    public MusicButton(MusicButtonConfig config) {
        var muted = Settings.MusicVolume.Value <= 0f;
        // Flash SoundIcon draws the full 16px tile at 2x (32px box). Removing
        // only the 1px atlas padding reproduces that tile; removing more
        // crops into the glyph and magnifies it.
        _musicOn = TextureHelper.FromGameAtlas("lofiInterfaceBig", 3, false);
        _musicOff = TextureHelper.FromGameAtlas("lofiInterfaceBig", 4, false);

        var iconConfig = new IconButtonConfig { Texture = muted ? _musicOff : _musicOn, Width = config.Width, Height = config.Height, OnClick = OnClick, Alpha = config.Alpha, Anchor = config.Anchor, GameObjectShade = false };
        _button = new IconButton(iconConfig);
        AddChild(_button);

        X = config.X;
        Y = config.Y;
        MouseEnabled = true;
        AddEventListener(MouseEvent.MouseOver, OnMouseOver);
        AddEventListener(MouseEvent.MouseOut, OnMouseOut);
    }

    private void OnClick() {
        if (Settings.MusicVolume.Value > 0f) {
            _lastVolume = Settings.MusicVolume.Value;
            Settings.SetMusicVolume(0f);
        } else {
            Settings.SetMusicVolume(_lastVolume <= 0f ? 1f : _lastVolume);
        }

        _button.ChangeTexture(Settings.MusicVolume.Value > 0f ? _musicOn : _musicOff);
        Audio.MusicChannel.SetVolume(Settings.GetMusicVolume());
        Settings.SaveSettings();
    }

    protected override void OnResize(ResizeEvent args) {
        Scale = Stage.ScreenScale;
    }

    private void OnMouseOver() => _button.SetColor(0xFFDC85);
    
    private void OnMouseOut() => _button.SetColor(0xFFFFFF);
    
}