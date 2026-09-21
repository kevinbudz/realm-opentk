using System.Xml.Linq;
using AlloyClient.Assets.XmlStructs;
using AlloyClient.Game;
using AlloyClient.Game.Components.Hud;
using AlloyClient.Game.Components.Hud.Panels;
using AlloyClient.Game.Objects;
using AlloyClient.Networking;
using AlloyClient.Networking.Enums;
using AlloyClient.Networking.Packets.Outgoing;
using AlloyClient.Networking.Structs.DataObjects;

namespace Alloy.UiLib.Tests;

// Flash parity for portals: the wire Active stat drives usability
// (GameServerConnection ACTIVE_STAT -> Portal.active_), the panel shows the
// live Name stat with the Locked prefix stripped (PortalPanel.draw via
// GameObject.getName), and GuildHallPortal opens a guild-gated panel.
internal static class PortalParityTests {
    public static void Run() {
        PortalUsableDefaultsTrue();
        ActiveStatDrivesPortalUsable();
        PortalNameUsesLiveName();
        LockedPrefixStripped();
        GuildHallPortalIsInteractive();
        GuildGateBlocksSend();
        GuildedPlayerSendsUsePortal();
        NexusPortalFlagParsed();
    }

    private static Entity Portal(string cls, int objectId = 9, string extraXml = "") {
        var xml = XElement.Parse(
            $"<Object type=\"1796\" id=\"Test {cls}\"><Class>{cls}</Class>{extraXml}</Object>");
        return new Entity { Properties = new ObjectProperties(xml), ObjectId = objectId };
    }

    private static StatData Stat(StatsType type, int value) =>
        new() { Type = type, Value = value };

    // Flash Portal.active_ defaults to true, as does realm-server
    // Portal.Usable: a fresh portal shows Enter, not Full.
    private static void PortalUsableDefaultsTrue() {
        Equal(true, new Entity().PortalUsable);
    }

    // Without the Active case the panel would always show Full: the server
    // never exports Usable as a stat, so 0/1 on the wire is the only signal.
    private static void ActiveStatDrivesPortalUsable() {
        var portal = Portal("Portal");
        portal.UpdateStats(new[] { Stat(StatsType.Active, 0) }, 0, 1);
        Equal(false, portal.PortalUsable);

        portal.UpdateStats(new[] { Stat(StatsType.Active, 1) }, 0, 1);
        Equal(true, portal.PortalUsable);
    }

    // Flash draw() reads owner_.getName(): the live Name stat (e.g. the
    // PortalMonitor "Realm (count)" rename), falling back to DisplayId.
    private static void PortalNameUsesLiveName() {
        var portal = Portal("Portal");
        Equal("Test Portal", PortalPanel.GetPortalName(portal));

        portal.Name = "Realm (3)";
        Equal("Realm (3)", PortalPanel.GetPortalName(portal));

        Equal("", PortalPanel.GetPortalName(null));
    }

    // Locked portals strip the leading "Locked " from the live name.
    private static void LockedPrefixStripped() {
        var locked = Portal("Portal", extraXml: "<LockedPortal/>");
        Equal(true, locked.Properties.LockedPortal);
        Equal("Wine Cellar", PortalPanel.GetPortalName(WithName(locked, "Locked Wine Cellar")));
        Equal("Wine Cellar Portal", PortalPanel.GetPortalName(WithName(locked, "Wine Cellar Portal")));

        var open = Portal("Portal");
        Equal("Locked Wine Cellar", PortalPanel.GetPortalName(WithName(open, "Locked Wine Cellar")));
    }

    private static Entity WithName(Entity entity, string name) {
        entity.Name = name;
        return entity;
    }

    // Flash GuildHallPortal implements IInteractiveObject with its own panel;
    // previously Alloy returned no panel (null) for the class.
    private static void GuildHallPortalIsInteractive() {
        Equal(true, InteractPanel.IsInteractiveObject(Portal("GuildHallPortal")));
        Equal(true, InteractPanel.IsInteractiveObject(Portal("Portal")));
        Equal(false, InteractPanel.IsInteractiveObject(Portal("Enemy")));
        Equal(null, InteractPanel.GetInteractPanel(null));
    }

    // Flash never wires the button or key for guildless players: no UsePortal
    // may go out (headless has no local player, so it must block).
    private static void GuildGateBlocksSend() {
        var previous = Map.LocalPlayer;
        Map.LocalPlayer = null;
        var sent = 0;
        Client.OutgoingSink = _ => sent++;
        try {
            Equal(false, GuildHallPortalPanel.HasGuild(null));
            Equal(false, GuildHallPortalPanel.HasGuild(new Player { Guild = "" }));
            Equal(false, GuildHallPortalPanel.TrySendUsePortal(Portal("GuildHallPortal", objectId: 51)));
            Equal(false, GuildHallPortalPanel.TrySendUsePortal(null));
        } finally {
            Client.OutgoingSink = null;
            Map.LocalPlayer = previous;
        }
        Equal(0, sent);
    }

    // A guilded player sends UsePortal with the portal's object id (the Enter
    // button and the Interact key share this path).
    private static void GuildedPlayerSendsUsePortal() {
        var previous = Map.LocalPlayer;
        Map.LocalPlayer = new Player { Guild = "Knights" };
        var sent = new List<int>();
        Client.OutgoingSink = pkt => {
            if (pkt is UsePortal u)
                sent.Add(u.ObjectId);
        };
        try {
            Equal(true, GuildHallPortalPanel.HasGuild(Map.LocalPlayer));
            Equal(true, GuildHallPortalPanel.TrySendUsePortal(Portal("GuildHallPortal", objectId: 52)));
        } finally {
            Client.OutgoingSink = null;
            Map.LocalPlayer = previous;
        }
        Equal(1, sent.Count);
        Equal(52, sent[0]);
    }

    // The Nexus Portal XML tag drives the world-space floating name; portals
    // without it parse false.
    private static void NexusPortalFlagParsed() {
        Equal(true, Portal("Portal", extraXml: "<NexusPortal/>").Properties.NexusPortal);
        Equal(false, Portal("Portal").Properties.NexusPortal);
    }

    private static void Equal<T>(T expected, T actual) {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new Exception($"expected {expected}, got {actual}");
    }
}
