using Alloy.UiLib.Core;

namespace AlloyClient.Game.Components.Hud;

public class CharacterBars : Sprite {

    // Flash StatMetersView uses 176x16 bars with 8px gaps between rows.
    public int height = 16;
    public int offset = 8;

    private const int BarWidth = 176;

    private readonly StatusBar _expBar;
    private readonly StatusBar _fameBar;
    private readonly StatusBar _hpBar;
    private readonly StatusBar _mpBar;

    public CharacterBars() {
        _expBar = new StatusBar(BarWidth, height, 5931045, 5526612, 0xFFFFFF, "Lvl X");
        _fameBar = new StatusBar(BarWidth, height, 14835456, 5526612, 0xFFFFFF, "Fame");
        _hpBar = new StatusBar(BarWidth, height, 14693428, 5526612, 0xFFFFFF, "HP");
        _mpBar = new StatusBar(BarWidth, height, 6325472, 5526612, 0xFFFFFF, "MP");
        _fameBar.Visible = false;
        AddChild(_hpBar);
        AddChild(_mpBar);
        AddChild(_fameBar);
        AddChild(_expBar);
        SetPositions();
    }

    private void SetPositions() {
        _fameBar.Y = 0;
        _expBar.Y = 0;
        _hpBar.Y = height + offset;
        _mpBar.Y = (height + offset) * 2;
    }

    public void Update() {
        var player = Map.LocalPlayer;

        var levelText = $"Lvl {player.Level}";
        if (_expBar.labelString != levelText) {
            _expBar.UpdateLabel(levelText);
        }

        if (player.Level != 20) {
            if (!_expBar.Visible) {
                _expBar.Visible = true;
                _fameBar.Visible = false;
            }

            _expBar.Update(player.Experience, player.NextLevelExp);
        }
        else {
            if (!_fameBar.Visible) {
                _fameBar.Visible = true;
                _expBar.Visible = false;
            }

            _fameBar.Update(player.CurrentFame, player.FameGoal);
        }

        _hpBar.Update(player.Hp, player.MaxHp, player.MaxHpBoost, player.Properties.PlayerProperties.MaxHp, player.Level);
        _mpBar.Update(player.Mp, player.MaxMp, player.MaxMpBoost, player.Properties.PlayerProperties.MaxMp, player.Level);
    }
}
