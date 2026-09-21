using System;
using AlloyClient.Assets.Libraries;
using AlloyClient.Game.Objects;
using AlloyClient.Networking;
using AlloyClient.Networking.Packets.Outgoing;
using AlloyClient.Ui;
using AlloyClient.Ui.Components.Buttons;
using AlloyClient.Ui.Components.Elements;
using AlloyClient.Ui.Flash;
using AlloyClient.Utils;
using Alloy.UiLib.BuiltIn;
using Alloy.UiLib.Core;

namespace AlloyClient.Game.Components.Hud.Panels;

// Flash parity (kabam.rotmg.game.view.SellableObjectPanel): the interact panel
// for SellableObject vendors (Merchant, GuildMerchant, ClosedVaultChest).
// 16px bold white centered name (WIDTH-44 wide at x=44, y=12 or y=0 when
// wrapped tall), vendor icon bitmap at (-4,-8), LegacyBuyButton("", 16)
// centered at y=HEIGHT-h/2-17, star-gated "Rank Required:" + RankText badge
// or red "X Rank Required" guild text centered at y=HEIGHT-h/2-20. Buy goes
// out on click or the Interact key; rank/guild-blocked vendors never send.
public class SellableObjectPanel : Panel
{

    // Flash parity (GuildMerchant sets guildRankReq_ = GuildUtil.LEADER, and
    // the server rejects hall upgrades below rank 30).
    public const int GuildLeaderRank = 30;

    private readonly Entity _entity;

    private readonly SimpleText _nameText;
    private readonly LegacyBuyButton _buyButton;
    private Sprite _rankReqText;
    private SimpleText _guildRankReqText;

    public SellableObjectPanel(Entity entity)
    {
        _entity = entity;

        _nameText = new SimpleText(new TextConfig
        {
            Text = SoldObjectName(_entity),
            FontSize = 16,
            FontType = FontType.Bold,
            Color = 0xFFFFFF,
            MaxWidth = PanelWidth - 44,
            DropShadow = FlashTextFilters.Default,
            Anchor = UiAnchor.MiddleTop
        });
        _nameText.X = (PanelWidth + 44) / 2;
        _nameText.Y = 12;
        AddChild(_nameText);

        _buyButton = new LegacyBuyButton("", 16, _entity.MerchandisePrice, _entity.MerchandiseCurrency, SendBuy);
        AddChild(_buyButton);

        try
        {
            var icon = new ObjectRect(new ObjectRectConfig
            {
                Texture = TextureHelper.Create(_entity.Texture, Alloy.UiLib.Core.TextureType.GameAtlas),
                Y = -4,
                Width = 40,
                Height = 40
            });
            icon.X = _nameText.X - (_nameText.Width / 2) - icon.Width - 4;
            AddChild(icon);
        }
        catch
        {
            // Headless tests and unknown merchandise types keep the base
            // sprite (Flash Merchant.setMerchandiseType never throws either).
        }

        AddEventListener(Event.AddedToStage, () => { AddEventListener(Event.EnterFrame, OnFrameEnter); });
        AddEventListener(Event.RemovedFromStage, () => { RemoveEventListener(Event.EnterFrame, OnFrameEnter); });
        Draw();
    }

    // Named action so the buy button, the Interact key, and tests share one
    // path. Rank/guild-blocked vendors never send (Flash removes the button).
    public void SendBuy()
    {
        TrySendBuy(_entity);
    }

    // Static so the gated send is unit-testable without constructing UI
    // (font/engine init is unavailable headless). Returns false when blocked.
    public static bool TrySendBuy(Entity entity)
    {
        if (entity == null || IsPurchaseBlocked(entity))
            return false;

        var pkt = Buy.CreatePacket();
        pkt.ObjectId = entity.ObjectId;
        Client.QueuePacket(pkt);
        return true;
    }

    protected override void OnInteractKey()
    {
        SendBuy();
    }

    private void Draw()
    {
        _nameText.SetText(SoldObjectName(_entity));
        _nameText.Y = _nameText.Height > 30 ? 0 : 12;

        var player = Map.LocalPlayer;
        var stars = player?.Stars ?? 0;
        var guildRank = player?.GuildRank ?? 0;
        var rankReq = _entity.MerchandiseRankReq;
        var guildRankReq = _entity.Properties?.Class == "GuildMerchant" ? GuildLeaderRank : -1;

        if (stars < rankReq)
        {
            if (Contains(_buyButton))
                RemoveChild(_buyButton);
            if (_guildRankReqText != null && Contains(_guildRankReqText))
            {
                RemoveChild(_guildRankReqText);
                _guildRankReqText = null;
            }
            if (_rankReqText == null || !Contains(_rankReqText))
            {
                if (_rankReqText != null)
                    RemoveChild(_rankReqText);
                _rankReqText = BuildRankReqText(rankReq);
                _rankReqText.X = PanelWidth / 2 - _rankReqText.Width / 2;
                _rankReqText.Y = PanelHeight - _rankReqText.Height / 2 - 20;
                AddChild(_rankReqText);
            }
        }
        else if (guildRank < guildRankReq)
        {
            if (Contains(_buyButton))
                RemoveChild(_buyButton);
            if (_rankReqText != null && Contains(_rankReqText))
            {
                RemoveChild(_rankReqText);
                _rankReqText = null;
            }
            if (_guildRankReqText == null || !Contains(_guildRankReqText))
            {
                _guildRankReqText = new SimpleText(new TextConfig
                {
                    Text = GuildUtils.RankToString(guildRankReq) + " Rank Required",
                    FontSize = 16,
                    FontType = FontType.Bold,
                    Color = 0xFF0000,
                    DropShadow = FlashTextFilters.Default,
                    Anchor = UiAnchor.MiddleTop
                });
                _guildRankReqText.X = PanelWidth / 2;
                AddChild(_guildRankReqText);
            }
            _guildRankReqText.Y = PanelHeight - _guildRankReqText.Height / 2 - 20;
        }
        else
        {
            if (_rankReqText != null && Contains(_rankReqText))
            {
                RemoveChild(_rankReqText);
                _rankReqText = null;
            }
            if (_guildRankReqText != null && Contains(_guildRankReqText))
            {
                RemoveChild(_guildRankReqText);
                _guildRankReqText = null;
            }
            _buyButton.SetPrice(_entity.MerchandisePrice, _entity.MerchandiseCurrency);
            _buyButton.SetEnabled(true);
            if (!Contains(_buyButton))
                AddChild(_buyButton);
            _buyButton.X = PanelWidth / 2 - _buyButton.Width / 2;
            _buyButton.Y = PanelHeight - _buyButton.Height / 2 - 17;
        }
    }

    private void OnFrameEnter()
    {
        Draw();
    }

    // Flash parity (SellableObjectPanel.createRankReqText): 16px bold white
    // "Rank Required:" plus the star badge, laid out as one row.
    private static Sprite BuildRankReqText(int rankReq)
    {
        var row = new Container(new ContainerConfig());
        var required = new SimpleText(new TextConfig
        {
            Text = "Rank Required:",
            FontSize = 16,
            FontType = FontType.Bold,
            Color = 0xFFFFFF,
            DropShadow = FlashTextFilters.Default
        });
        row.AddChild(required);
        var badge = new RankText(rankReq, largeText: false, includePrefix: false);
        badge.X = required.Width + 4;
        badge.Y = (required.Height - badge.Height) / 2;
        row.AddChild(badge);
        return row;
    }

    public static bool IsPurchaseBlocked(Entity entity)
    {
        return BlockedReason(entity) != null;
    }

    public static string BlockedReason(Entity entity)
    {
        if (entity == null)
            return null;

        var player = Map.LocalPlayer;
        var playerStars = player?.Stars ?? 0;
        if (playerStars < entity.MerchandiseRankReq)
            return "Rank Required";

        // Flash parity: only GuildMerchant carries a guild-rank requirement
        // (LEADER); Merchant and ClosedVaultChest default to none (-1).
        if (entity.Properties?.Class == "GuildMerchant")
        {
            var guildRank = player?.GuildRank ?? 0;
            if (guildRank < GuildLeaderRank)
                return "Leader Rank Required";
        }

        return null;
    }

    // Flash parity (Merchant/GuildMerchant/ClosedVaultChest soldObjectName):
    // merchants name the sold item, hall upgrades name themselves, and the
    // vault chest sells a "Vault Chest".
    public static string SoldObjectName(Entity entity)
    {
        if (entity?.Properties == null)
            return "";

        return entity.Properties.Class switch
        {
            "Merchant" => entity.MerchandiseType > 0 && entity.MerchandiseType <= ushort.MaxValue
                ? ObjectLibrary.GetDisplayName((ushort)entity.MerchandiseType)
                : entity.Properties.DisplayName,
            "ClosedVaultChest" => "Vault Chest",
            _ => entity.Properties.DisplayName
        };
    }

    // Flash parity (com.company.assembleegameclient.util.Currency.typeToName).
    public static string CurrencyName(int currency)
    {
        return currency switch
        {
            0 => "Gold",
            1 => "Fame",
            2 => "Guild Fame",
            _ => ""
        };
    }

    public static string PriceText(int price, int currency)
    {
        var name = CurrencyName(currency);
        return string.IsNullOrEmpty(name) ? price.ToString() : $"{price} {name}";
    }
}
