using System.Xml.Linq;
using Alloy.UiLib;
using AlloyClient.Assets.XmlStructs;
using Microsoft.Extensions.Logging.Abstractions;
using AlloyClient.Game.Components.Hud;
using AlloyClient.Game.Components.Hud.Panels;
using AlloyClient.Game.Objects;
using AlloyClient.Networking;
using AlloyClient.Networking.Enums;
using AlloyClient.Networking.Packets.Outgoing;
using AlloyClient.Networking.Structs.DataObjects;

namespace Alloy.UiLib.Tests;

// Flash parity for shop vendors: merchant stat mapping (GameServerConnection
// MERCHANDISE_* handlers), interact dispatch (IInteractiveObject), the
// SellableObjectPanel Buy flow, and the Nexus Store_3/5/6 region wiring.
internal static class ShopParityTests {
    public static void Run() {
        // The event system logs through the engine logger factory, which is
        // only set during engine init; headless tests get a null factory.
        UiRender.LogFactory ??= NullLoggerFactory.Instance;
        MerchantStatsMapToFields();
        MerchantTextureRefreshIsSafe();
        InteractDispatchCoversVendors();
        SoldObjectNames();
        CurrencyNames();
        SendBuySendsBuyPacket();
        RankBlockedVendorSendsNothing();
    }

    private static Entity Vendor(string cls, int objectId = 7) {
        var xml = XElement.Parse(
            $"<Object type=\"458\" id=\"Test {cls}\"><Class>{cls}</Class></Object>");
        return new Entity { Properties = new ObjectProperties(xml), ObjectId = objectId };
    }

    private static StatData Stat(StatsType type, int value) =>
        new() { Type = type, Value = value };

    // GameServerConnection.as MERCHANDISE_TYPE/PRICE/CURRENCY/COUNT/MINS_LEFT/
    // DISCOUNT/RANK_REQ_STAT handlers map onto Merchant/SellableObject fields.
    private static void MerchantStatsMapToFields() {
        var merchant = Vendor("Merchant");
        var stats = new[] {
            Stat(StatsType.MerchandiseType, 0x0ca0),
            Stat(StatsType.MerchandisePrice, 500),
            Stat(StatsType.MerchandiseCurrency, 0),
            Stat(StatsType.MerchandiseCount, 3),
            Stat(StatsType.MerchandiseMinsLeft, 5),
            Stat(StatsType.MerchandiseDiscount, 10),
            Stat(StatsType.MerchandiseRankReq, 2),
        };
        merchant.UpdateStats(stats, 0, stats.Length);

        Equal(0x0ca0, merchant.MerchandiseType);
        Equal(500, merchant.MerchandisePrice);
        Equal(0, merchant.MerchandiseCurrency);
        Equal(3, merchant.MerchandiseCount);
        Equal(5, merchant.MerchandiseMinsLeft);
        Equal(10, merchant.MerchandiseDiscount);
        Equal(2, merchant.MerchandiseRankReq);
    }

    // Merchant.setMerchandiseType resolves the sold item's texture; unknown
    // types must keep the base texture (never throw), including headless
    // without a render type or atlas entries.
    private static void MerchantTextureRefreshIsSafe() {
        var merchant = Vendor("Merchant");
        merchant.MerchandiseType = 0x0ca0;
        merchant.RefreshMerchandiseTexture();

        var chest = Vendor("ClosedVaultChest");
        chest.MerchandiseType = 0x0ca0;
        chest.RefreshMerchandiseTexture();
    }

    // Flash GameSprite.updateNearestInteractive casts goDict_ entries to
    // IInteractiveObject; SellableObject subclasses qualify.
    private static void InteractDispatchCoversVendors() {
        // Panel construction needs engine font init (unavailable headless, so
        // GetInteractPanel's vendor arms mirror this predicate by inspection);
        // everything assertable without UI construction is covered here.
        foreach (var cls in new[] { "Merchant", "GuildMerchant", "ClosedVaultChest" })
            Equal(true, InteractPanel.IsInteractiveObject(Vendor(cls)));

        Equal(true, InteractPanel.IsInteractiveObject(Vendor("Container")));
        Equal(true, InteractPanel.IsInteractiveObject(Vendor("Portal")));
        Equal(false, InteractPanel.IsInteractiveObject(Vendor("Enemy")));
        Equal(null, InteractPanel.GetInteractPanel(Vendor("Enemy")));
        Equal(null, InteractPanel.GetInteractPanel(null));
    }

    // Flash soldObjectName: merchants name the merchandise, hall upgrades
    // name themselves, the vault chest sells a "Vault Chest".
    private static void SoldObjectNames() {
        Equal("Vault Chest", SellableObjectPanel.SoldObjectName(Vendor("ClosedVaultChest")));

        var hall = Vendor("GuildMerchant");
        Equal("Test GuildMerchant", SellableObjectPanel.SoldObjectName(hall));

        var merchant = Vendor("Merchant");
        Equal("Test Merchant", SellableObjectPanel.SoldObjectName(merchant));
    }

    // Flash Currency.typeToName: 0 Gold, 1 Fame, 2 Guild Fame.
    private static void CurrencyNames() {
        Equal("Gold", SellableObjectPanel.CurrencyName(0));
        Equal("Fame", SellableObjectPanel.CurrencyName(1));
        Equal("Guild Fame", SellableObjectPanel.CurrencyName(2));
        Equal("", SellableObjectPanel.CurrencyName(99));
        Equal("500 Gold", SellableObjectPanel.PriceText(500, 0));
        Equal("2000 Fame", SellableObjectPanel.PriceText(2000, 1));
    }

    // The gated Buy action dispatches Buy with the vendor's object id (the
    // buy button and the Interact key share it via SendBuy).
    private static void SendBuySendsBuyPacket() {
        var merchant = Vendor("Merchant", objectId: 42);
        var sent = new List<int>();
        Client.OutgoingSink = pkt => {
            if (pkt is Buy b)
                sent.Add(b.ObjectId);
        };
        try {
            Equal(true, SellableObjectPanel.TrySendBuy(merchant));
        } finally {
            Client.OutgoingSink = null;
        }
        Equal(1, sent.Count);
        Equal(42, sent[0]);
        Equal(false, SellableObjectPanel.TrySendBuy(null));
    }

    // Flash draw() removes the buy button below the star requirement; the
    // gated action must not send either.
    private static void RankBlockedVendorSendsNothing() {
        var merchant = Vendor("Merchant", objectId: 43);
        merchant.MerchandiseRankReq = 5;
        Equal(true, SellableObjectPanel.IsPurchaseBlocked(merchant));
        Equal("Rank Required", SellableObjectPanel.BlockedReason(merchant));

        var sent = 0;
        Client.OutgoingSink = _ => sent++;
        try {
            Equal(false, SellableObjectPanel.TrySendBuy(merchant));
        } finally {
            Client.OutgoingSink = null;
        }
        Equal(0, sent);

        var hall = Vendor("GuildMerchant", objectId: 44);
        Equal("Leader Rank Required", SellableObjectPanel.BlockedReason(hall));
    }

    private static void Equal<T>(T expected, T actual) {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new Exception($"expected {expected}, got {actual}");
    }
}
