using System.Xml.Linq;
using Alloy.UiLib;
using AlloyClient.Assets.XmlStructs;
using Microsoft.Extensions.Logging.Abstractions;
using AlloyClient.Game;
using AlloyClient.Game.Components.Hud;
using AlloyClient.Game.Components.Hud.Panels;
using AlloyClient.Game.Objects;
using AlloyClient.Networking;
using AlloyClient.Networking.Enums;
using AlloyClient.Networking.Packets.Incoming;
using AlloyClient.Networking.Packets.Outgoing;
using AlloyClient.Networking.Structs.DataObjects;

namespace Alloy.UiLib.Tests;

// Flash parity for the Nexus changers: the NameChosen stat drives
// player.nameChosen_ (GameServerConnection NAME_CHOSEN_STAT), NameChanger /
// CharacterChanger are interactive (IInteractiveObject), the NameChangerPanel
// branches buy/choose/rank-required and sends ChooseName, NameResult applies
// the pending name or surfaces the error, and the CharacterChangerPanel
// leaves the game like gs_.closed.
internal static class ChangerParityTests {
    public static void Run() {
        // The event system logs through the engine logger factory, which is
        // only set during engine init; headless tests get a null factory.
        UiRender.LogFactory ??= NullLoggerFactory.Instance;
        NameChosenStatMapsToPlayer();
        InteractDispatchCoversChangers();
        PanelBranches();
        NameValidation();
        SendChooseNameSendsPacket();
        InvalidNameSendsNothing();
        NameResultSuccessAppliesName();
        NameResultFailureKeepsError();
        CharacterChangeRequestsLeave();
    }

    private static Entity Changer(string cls) {
        var xml = XElement.Parse(
            $"<Object type=\"458\" id=\"Test {cls}\"><Class>{cls}</Class></Object>");
        return new Entity { Properties = new ObjectProperties(xml), ObjectId = 7 };
    }

    private static StatData Stat(StatsType type, int value) =>
        new() { Type = type, Value = value };

    // GameServerConnection.as NAME_CHOSEN_STAT maps onto player.nameChosen_.
    private static void NameChosenStatMapsToPlayer() {
        var player = new Player();
        Equal(true, player.NameChosen);

        player.UpdateStats(new[] { Stat(StatsType.NameChosen, 0) }, 0, 1);
        Equal(false, player.NameChosen);

        player.UpdateStats(new[] { Stat(StatsType.NameChosen, 1) }, 0, 1);
        Equal(true, player.NameChosen);
    }

    // Flash GameSprite.updateNearestInteractive casts goDict_ entries to
    // IInteractiveObject; NameChanger/CharacterChanger qualify.
    private static void InteractDispatchCoversChangers() {
        // Panel construction needs engine font init (unavailable headless, so
        // GetInteractPanel's changer arms mirror this predicate by inspection);
        // everything assertable without UI construction is covered here.
        Equal(true, InteractPanel.IsInteractiveObject(Changer("NameChanger")));
        Equal(true, InteractPanel.IsInteractiveObject(Changer("CharacterChanger")));

        Equal(true, InteractPanel.IsInteractiveObject(Changer("Container")));
        Equal(true, InteractPanel.IsInteractiveObject(Changer("Portal")));
        Equal(false, InteractPanel.IsInteractiveObject(Changer("Enemy")));
        Equal(null, InteractPanel.GetInteractPanel(Changer("Enemy")));
        Equal(null, InteractPanel.GetInteractPanel(null));
    }

    // Flash NameChangerPanel ctor branches: buy when named, rank text when
    // below the requirement, choose otherwise.
    private static void PanelBranches() {
        Equal(NameChangerPanel.NamePanelState.Buy,
            NameChangerPanel.GetPanelState(nameChosen: true, stars: 0, rankRequired: 0));
        Equal(NameChangerPanel.NamePanelState.Buy,
            NameChangerPanel.GetPanelState(nameChosen: true, stars: 0, rankRequired: 5));
        Equal(NameChangerPanel.NamePanelState.RankRequired,
            NameChangerPanel.GetPanelState(nameChosen: false, stars: 0, rankRequired: 5));
        Equal(NameChangerPanel.NamePanelState.Choose,
            NameChangerPanel.GetPanelState(nameChosen: false, stars: 5, rankRequired: 5));
        Equal(NameChangerPanel.NamePanelState.Choose,
            NameChangerPanel.GetPanelState(nameChosen: false, stars: 0, rankRequired: 0));

        Equal(1000, NameChangerPanel.NameChangePrice);
        Equal("Change (1000 Gold)", NameChangerPanel.GetActionText(nameChosen: true));
        Equal("Choose", NameChangerPanel.GetActionText(nameChosen: false));
        Equal("Choose Account Name", NameChangerPanel.GetTitleText(null, nameChosen: false));
        Equal("Your name is:\nBob", NameChangerPanel.GetTitleText("Bob", nameChosen: true));
    }

    // Flash ChooseNameFrame nameInput restrict A-Za-z, maxChars 10.
    private static void NameValidation() {
        Equal(true, NameChangerPanel.IsValidName("Bob"));
        Equal(true, NameChangerPanel.IsValidName("Abcdefghij"));
        Equal(false, NameChangerPanel.IsValidName(null));
        Equal(false, NameChangerPanel.IsValidName(""));
        Equal(false, NameChangerPanel.IsValidName("Abcdefghijk"));
        Equal(false, NameChangerPanel.IsValidName("Bob1"));
        Equal(false, NameChangerPanel.IsValidName("A b"));
        Equal(false, NameChangerPanel.IsValidName("Ab!"));
    }

    // The gated ChooseName action queues ChooseName with the typed name and
    // records it pending (ChooseNameFrameMediator.name); the button and the
    // Interact key share it via SubmitName.
    private static void SendChooseNameSendsPacket() {
        NameChangerPanel.PendingName = null;
        NameChangerPanel.LastError = "stale";
        var sent = new List<string>();
        Client.OutgoingSink = pkt => {
            if (pkt is ChooseName c)
                sent.Add(c.Name);
        };
        try {
            Equal(true, NameChangerPanel.TrySendChooseName("Bob"));
        } finally {
            Client.OutgoingSink = null;
        }
        Equal(1, sent.Count);
        Equal("Bob", sent[0]);
        Equal("Bob", NameChangerPanel.PendingName);
        Equal(null, NameChangerPanel.LastError);
        NameChangerPanel.PendingName = null;
    }

    // Invalid names never send (Flash prevents them at keystroke).
    private static void InvalidNameSendsNothing() {
        NameChangerPanel.PendingName = null;
        var sent = 0;
        Client.OutgoingSink = _ => sent++;
        try {
            Equal(false, NameChangerPanel.TrySendChooseName("Bob1"));
            Equal(false, NameChangerPanel.TrySendChooseName(""));
            Equal(false, NameChangerPanel.TrySendChooseName(null));
        } finally {
            Client.OutgoingSink = null;
        }
        Equal(0, sent);
        Equal(null, NameChangerPanel.PendingName);
        Equal("Invalid name.", NameChangerPanel.LastError);
        NameChangerPanel.LastError = null;
    }

    // Flash ChooseNameFrameMediator.handleSuccessfulNameChange: the model
    // name is set and the dialog state clears (also via NameResult.Handle).
    private static void NameResultSuccessAppliesName() {
        var previous = Map.LocalPlayer;
        Map.LocalPlayer = new Player { NameChosen = false };
        NameChangerPanel.PendingName = "Bob";
        try {
            new NameResult { Success = true, ErrorText = "" }.Handle();
        } finally {
            Equal("Bob", Map.LocalPlayer.Name);
            Equal(true, Map.LocalPlayer.NameChosen);
            Equal(null, NameChangerPanel.PendingName);
            Equal(null, NameChangerPanel.LastError);
            Map.LocalPlayer = previous;
        }
    }

    // Flash handleFailedNameChange: the error is shown and the typed name
    // is kept for retry.
    private static void NameResultFailureKeepsError() {
        var previous = Map.LocalPlayer;
        Map.LocalPlayer = new Player { NameChosen = false };
        NameChangerPanel.PendingName = "Bob";
        try {
            new NameResult { Success = false, ErrorText = "Name already taken." }.Handle();
        } finally {
            Equal("Name already taken.", NameChangerPanel.LastError);
            Equal("Bob", NameChangerPanel.PendingName);
            Equal(false, Map.LocalPlayer.NameChosen);
            Map.LocalPlayer = previous;
            NameChangerPanel.PendingName = null;
            NameChangerPanel.LastError = null;
        }
    }

    // Flash CharacterChangerPanel dispatches gs_.closed, which leaves the
    // game for character selection.
    private static void CharacterChangeRequestsLeave() {
        Equal("Change Characters", CharacterChangerPanel.TitleText);
        Equal("Change", CharacterChangerPanel.ButtonText);

        var closed = 0;
        CharacterChangerPanel.RequestCharacterChange(() => closed++);
        Equal(1, closed);
    }

    private static void Equal<T>(T expected, T actual) {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new Exception($"expected {expected}, got {actual}");
    }
}
