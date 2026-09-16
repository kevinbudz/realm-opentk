using Alloy.UiLib.BuiltIn;
using Alloy.UiLib.Core;
using Alloy.UiLib.Extra;
using AlloyClient.Game.Objects;
using AlloyClient.Networking;
using AlloyClient.Networking.Packets.Outgoing;
using AlloyClient.Ui;
using AlloyClient.Ui.Flash;
using AlloyClient.Utils;

namespace AlloyClient.Game.Components.Hud.Inventory;

public sealed class PotionInventory : Sprite {
    private const int ButtonWidth = 84;
    private const int ButtonGap = 4;

    private readonly PotionSlot _health;
    private readonly PotionSlot _magic;

    public PotionInventory(Player player) {
        _health = new PotionSlot(player, 0x0A22, 254, CutEdges.Left);
        AddChild(_health);

        _magic = new PotionSlot(player, 0x0A23, 255, CutEdges.Right) { X = ButtonWidth + ButtonGap };
        AddChild(_magic);

        Refresh();
    }

    public void Refresh() {
        _health.SetCount(_health.Player.HealthStackCount);
        _magic.SetCount(_magic.Player.MagicStackCount);
    }

    private sealed class PotionSlot : Sprite {
        private const int ButtonHeight = 24;

        public readonly Player Player;

        private readonly byte _slotId;
        private readonly CutEdgeRect _outer;
        private readonly CutEdgeRect _inner;
        private readonly ObjectRect _icon;
        private readonly SimpleText _countText;
        private readonly Timer _doubleClickTimer = new(250, 1);

        private int _count = -1;
        private bool _pendingSecondClick;

        public PotionSlot(Player player, ushort objectType, byte slotId, CutEdges cuts) {
            Player = player;
            _slotId = slotId;

            _outer = new CutEdgeRect(new CutEdgeConfig {
                Width = ButtonWidth,
                Height = ButtonHeight,
                CutX = 4,
                CutY = 4,
                Cuts = cuts,
                Color = 0x3E3D3D,
                MouseEnabled = true
            });
            _outer.AddEventListener(MouseEvent.LeftUp, OnMouseUp);
            _outer.AddEventListener(MouseEvent.MouseOut, CancelPendingClick);
            AddChild(_outer);

            _inner = new CutEdgeRect(new CutEdgeConfig {
                X = 2,
                Y = 2,
                Width = ButtonWidth - 4,
                Height = ButtonHeight - 4,
                CutX = 4,
                CutY = 4,
                Cuts = cuts,
                Color = 0x242222
            });
            AddChild(_inner);

            // Flash PotionSlotView renders getRedrawnTextureFromType(size 55),
            // a 22px visual, via a 46x35 bitmap at (13,-11), so the visual sits
            // at (25,1) with room for the black outline. Use a padded 28px
            // quad (22.4px visual + 2.8px border) at (22,-2) so the outline
            // has room instead of clipping.
            _icon = new ObjectRect(new ObjectRectConfig {
                Texture = TextureHelper.FromGameAtlas(objectType),
                X = 22,
                Y = -2,
                Width = 28,
                Height = 28
            });
            AddChild(_icon);

            _countText = new SimpleText(new TextConfig {
                Text = "0",
                FontSize = 13,
                FontType = FontType.Bold,
                X = ButtonWidth / 2 + 6,
                Y = 6,
                Color = 0xAAAAAA,
                DropShadow = FlashTextFilters.StrongOutline
            });
            AddChild(_countText);

            _doubleClickTimer.AddEventListener(TimerEvent.TimerComplete, CancelPendingClick);
        }

        public void SetCount(int count) {
            if (_count == count) return;

            _count = count;
            _countText.SetText(count.ToString());
            _countText.SetColor(count > 0 ? 0xFFFFFFu : 0xAAAAAAu);
            _outer.SetColor(count > 0 ? 0x545454u : 0x3E3D3Du);
            _inner.Visible = count <= 0;
            _icon.ColorTransformation = count > 0 ? Transforms.Default : Transforms.Dark;
        }

        private void OnMouseUp(MouseEvent @event) {
            if (_count <= 0) return;

            if (@event.ShiftKey || _pendingSecondClick) {
                CancelPendingClick();
                UsePotion();
                return;
            }

            _pendingSecondClick = true;
            _doubleClickTimer.Reset();
            _doubleClickTimer.Start();
        }

        private void CancelPendingClick() {
            _pendingSecondClick = false;
            _doubleClickTimer.Stop();
        }

        private void UsePotion() {
            var packet = UseItem.CreatePacket();
            packet.Time = (int)Map.LastGameTime.TotalMs;
            packet.SlotObject.ObjectId = Player.ObjectId;
            packet.SlotObject.SlotId = _slotId;
            packet.ItemUsePos.X = Player.Position.X;
            packet.ItemUsePos.Y = Player.Position.Y;
            packet.UseType = (byte)UseType.START_USE;
            Client.QueuePacket(packet);
        }
    }
}
