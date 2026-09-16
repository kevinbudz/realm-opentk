using AlloyClient.Game.Objects;
using Alloy.UiLib.BuiltIn;
using Alloy.UiLib.Core;
using AlloyClient.Ui.Components.Buttons;
using AlloyClient.Utils;

namespace AlloyClient.Game.Components.Hud.Inventory
{
    public sealed class TabStrip : Sprite
    {
        private const int TabWidth = 28;
        private const int TabHeight = 35;
        private const int TabTopOffset = 27;
        private const int TabXPadding = 2;
        private const int TabYPadding = 8;
        private const int PanelWidth = 186;
        private const int PanelHeight = 126;
        private const int ContentPadding = 7;

        private enum TabTypes
        {
            None,
            Inventory,
            StatsView,
            Backpack,
            PetInfo
        }

        private const uint SelectedTabColor = 2368034;
        private const uint TabColor = 7039594;

        public int currentTabIndex = (int)TabTypes.Inventory;

        private readonly Entity _owner;
        private int _nextTabX;

        private readonly RoundedRect _inventoryTab;
        private readonly RoundedRect _statsTab;
        private RoundedRect _backpackTab;
        private readonly CutEdgeRect _contentBackground;

        private readonly InventoryGrid _inventoryGrid;
        private InventoryGrid _backpackPanel;
        private readonly StatsPanel _statsPanel;

        public TabStrip(Entity owner)
        {
            _owner = owner;

            _inventoryTab = AddTab(TabTypes.Inventory);
            _statsTab = AddTab(TabTypes.StatsView);

            // Flash's container begins 27px below the strip origin and covers
            // the lower edge of each 35px tab.
            _contentBackground = new CutEdgeRect(new CutEdgeConfig
            {
                Y = TabTopOffset,
                Width = PanelWidth,
                Height = PanelHeight,
                CutX = 6,
                CutY = 6,
                Cuts = CutEdges.All,
                Color = SelectedTabColor
            });
            AddChild(_contentBackground);

            // Tab content is retained for the lifetime of this strip. Recreating it
            // during selection used to clear every InventoryUpdate listener on the
            // owner, including the equipment grid and the newly-created grids.
            _inventoryGrid = new InventoryGrid(_owner, 4, false);
            _inventoryGrid.X = ContentPadding;
            _inventoryGrid.Y = TabTopOffset + ContentPadding;
            AddChild(_inventoryGrid);

            _statsPanel = new StatsPanel(_owner as Player);
            _statsPanel.Y = TabTopOffset + (PanelHeight - StatsPanel.PanelHeight) / 2;
            AddChild(_statsPanel);

            if (_owner is Player player && player.HasBackPack)
            {
                AddBackpackTab();
            }

            // HasBackPack arrives as player stat data and does not have a dedicated
            // signal yet. Polling this presentation flag avoids rebuilding existing
            // views when the server grants a backpack during play.
            AddEventListener(Event.EnterFrame, OnFrameEnter);
            AddEventListener(Event.Added, OnAdded);
            AddEventListener(Event.Removed, OnRemoved);
            SetTabVisibility(currentTabIndex);
        }

        private RoundedRect AddTab(TabTypes tabType)
        {
            var tabKey = (int)tabType;
            // Flash TabStripView draws tabs with drawRoundRect(28,35,radius 9).
            var tab = new RoundedRect(new RoundedRectConfig
            {
                Width = TabWidth,
                Height = TabHeight,
                Radius = 9,
                Corners = CutEdges.Top,
                Color = currentTabIndex == tabKey ? SelectedTabColor : TabColor,
                MouseEnabled = true
            });
            tab.X = _nextTabX;
            tab.Y = TabYPadding;
            tab.AddEventListener(MouseEvent.LeftUp, () => OnTabSelected(tabType));

            // Flash TabView places a 40px IconFactory bitmap at (-5,-11) inside
            // the 28x35 tab, with a 16px visual at (7,1) relative to the tab.
            // Use a padded 18px quad so the 16px visual keeps 1px for outline.
            var button = new ObjectRect(new ObjectRectConfig
            {
                Texture = TextureHelper.FromGameAtlas("lofiInterfaceBig", 23 + tabKey),
                X = _nextTabX + 6,
                Y = TabYPadding,
                Width = 18,
                Height = 18,
                MouseEnabled = true
            });
            button.AddEventListener(MouseEvent.LeftUp, () => OnTabSelected(tabType));

            // Tabs live behind the content container so its top edge masks the
            // lower 16px of each tab, matching Flash's display-list order.
            if (_contentBackground == null)
            {
                AddChild(tab);
                AddChild(button);
            }
            else
            {
                var containerIndex = GetChildIndex(_contentBackground);
                AddChildAt(tab, containerIndex);
                AddChildAt(button, containerIndex + 1);
            }

            _nextTabX = tab.X + TabWidth + TabXPadding;
            return tab;
        }

        private void AddBackpackTab()
        {
            if (_backpackPanel != null)
            {
                return;
            }

            _backpackTab = AddTab(TabTypes.Backpack);
            _backpackPanel = new InventoryGrid(_owner, 12, false, true);
            _backpackPanel.X = ContentPadding;
            _backpackPanel.Y = TabTopOffset + ContentPadding;
            AddChild(_backpackPanel);
            SetTabVisibility(currentTabIndex);
        }

        private void OnFrameEnter()
        {
            if (_owner is Player player && player.HasBackPack)
            {
                AddBackpackTab();
            }

            _statsPanel.Refresh();
            _inventoryGrid.RefreshPotionCounts();
            _backpackPanel?.RefreshPotionCounts();
        }

        private void OnAdded()
        {
            AddEventListener(Event.EnterFrame, OnFrameEnter);
        }

        private void OnRemoved(Event @event)
        {
            // Removed bubbles from descendants. Only tear down this strip when the
            // strip itself is detached from its parent.
            if (@event.Target != this)
            {
                return;
            }

            RemoveEventListener(Event.EnterFrame, OnFrameEnter);
        }

        private void OnTabSelected(TabTypes tabType)
        {
            currentTabIndex = (int)tabType;
            SetTabVisibility(currentTabIndex);
        }

        private void SetTabVisibility(int tabType)
        {
            _inventoryGrid.Visible = tabType == (int)TabTypes.Inventory;
            _statsPanel.Visible = tabType == (int)TabTypes.StatsView;
            _inventoryTab.SetColor(tabType == (int)TabTypes.Inventory ? SelectedTabColor : TabColor);
            _statsTab.SetColor(tabType == (int)TabTypes.StatsView ? SelectedTabColor : TabColor);

            if (_backpackPanel != null)
            {
                _backpackPanel.Visible = tabType == (int)TabTypes.Backpack;
                _backpackTab.SetColor(tabType == (int)TabTypes.Backpack ? SelectedTabColor : TabColor);
            }
        }
    }

    public class StatsPanel : Sprite
    {
        public const int PanelHeight = 45;

        private readonly Player _player;
        private readonly SimpleText[] _statValues = new SimpleText[6];

        public StatsPanel(Player player)
        {
            _player = player;
            string[] indexNames = { "ATT", "DEF", "SPD", "DEX", "VIT", "WIS" };

            for (var i = 0; i < indexNames.Length; i++)
            {
                var column = i % 2;
                var row = i / 2;
                // Rows +4 confirmed against Flash reference slice.
                var x = column == 0 ? 52 : 148;
                var y = row * 15 + 4;

                var statName = new SimpleText(new TextConfig
                {
                    Text = indexNames[i] + " -",
                    FontSize = 13,
                    FontType = FontType.Normal,
                    X = x - 1,
                    Y = y,
                    OutlineThickness = 0,
                    Color = 0xB3B3B3,
                    OutlineColor = 0,
                    Anchor = UiAnchor.RightTop
                });
                AddChild(statName);

                var statValue = new SimpleText(new TextConfig
                {
                    Text = "0",
                    FontSize = 13,
                    FontType = FontType.Bold,
                    X = x + 1,
                    Y = y,
                    OutlineThickness = 0,
                    Color = 0xB3B3B3,
                    OutlineColor = 0
                });
                AddChild(statValue);
                _statValues[i] = statValue;
            }

            Refresh();
        }

        public void Refresh()
        {
            if (_player == null)
            {
                return;
            }

            _statValues[0].SetText(_player.Attack.ToString());
            _statValues[1].SetText(_player.Defense.ToString());
            _statValues[2].SetText(_player.Speed.ToString());
            _statValues[3].SetText(_player.Dexterity.ToString());
            _statValues[4].SetText(_player.Vitality.ToString());
            _statValues[5].SetText(_player.Wisdom.ToString());
        }
    }
}
