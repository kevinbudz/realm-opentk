using AlloyClient.Ui.Components.Elements;
using Alloy.UiLib.BuiltIn;
using Alloy.UiLib.Core;

namespace AlloyClient.Game.Components.Hud.Chat;

public class ChatBoxLineData {
    public readonly double Time;
    public readonly string Name;
    public readonly int NumStars;
    public readonly string Recipient;
    public readonly bool ToMe;
    public readonly string Text;

    public readonly ChatBoxLine Sprite;

    public ChatBoxLineData(double time, string name, int numStars, string recipient, string text) {
        Time = time;
        Name = name;
        NumStars = numStars;
        Recipient = recipient;
        ToMe = recipient == Map.LocalPlayer?.Name;
        Text = text;

        Sprite = new ChatBoxLine(this);
    }
}

public class ChatBoxLine : Container {
    
    private const string ServerChatName = "";
    private const string ClientChatName = "*Client*";
    private const string ErrorChatName = "*Error*";
    private const string HelpChatName = "*Help*";
    private const string GuildChatName = "*Guild*";
    private const char EnemyNameChar = '#';
    private const char AdminNameChar = '@';

    private const uint DefaultColor = 0xFFFFFF;
    private const uint PlayerColor = 0x00FF00;
    private const uint ServerColor = 0xFFFF00;
    private const uint ClientColor = 0x0000FF;
    private const uint ErrorColor = 0xFF0000;
    private const uint HelpColor = 0xFF5B05;
    private const uint GuildColor = 0xA6FF5D;
    private const uint EnemyColor = 0xFFA800;
    private const uint AdminColor = 0xFFFF00;
    private const uint TellColor = 0x00F0FF;

    // Flash TextBoxLine.as / TextBox.as / ElementFormats.as: bold 14pt text,
    // a 16px minimum line height, and wrapped lines indented by 20px.
    internal const int FontSize = 14;
    internal const int MinLineHeight = 16;
    internal const int Indent = 20;
    internal const uint SeparatorColor = 0x363636;

    public ChatBoxLine(ChatBoxLineData data) : base(new ContainerConfig { Width = ChatBox.MaxWidth, Height = MinLineHeight }) {
        var x = 0;

        if (ShouldShowStar(data.NumStars, data.Recipient, data.ToMe)) {
            var fameStar = new FameStar(MinLineHeight, data.NumStars);
            fameStar.Y = 3;
            AddChild(fameStar);

            x += fameStar.Width + 2;
        }

        if (TryGetPm(data, out var pm)) {
            pm.X = x;
            pm.Y = 2;
            AddChild(pm);
            x += pm.Width;
        }

        if (TryGetDisplayName(data.Name, data.Recipient, data.ToMe, out var displayName, out var nameColor)) {
            var name = CreateText($"<{displayName}>", nameColor);
            name.X = x;
            name.Y = 2;
            AddChild(name);
            x += name.Width;

            var separator = CreateText(" ", SeparatorColor);
            separator.X = x;
            separator.Y = 2;
            AddChild(separator);
            x += separator.Width;
        }

        var color = GetMessageColor(data.Name, data.Recipient);
        var text = CreateText(data.Text, color, ChatBox.MaxWidth - x);

        text.X = x;
        text.Y = 2;
        text.OffsetLineWrapBy(Indent - x);
        AddChild(text);
    }

    // Flash TextBoxLine.getTextBlock keeps the rank icon for guild chat and
    // clears it only for outgoing tells (recipient shown instead of the name).
    internal static bool ShouldShowStar(int numStars, string recipient, bool toMe) {
        if (numStars < 0) {
            return false;
        }

        if (!toMe && recipient != string.Empty && recipient != GuildChatName) {
            return false;
        }

        return true;
    }

    // Flash shows the "To: " prefix only on outgoing tells.
    internal static bool ShouldShowPm(string recipient, bool toMe) {
        return !toMe && recipient != string.Empty && recipient != GuildChatName;
    }

    private static bool TryGetPm(ChatBoxLineData data, out SimpleText text) {
        if (!ShouldShowPm(data.Recipient, data.ToMe)) {
            text = null;
            return false;
        }

        text = CreateText("To: ", DefaultColor);
        return true;
    }

    // Flash TextBoxLine.getTextBlock: system senders show no name; the name
    // keeps the sender-derived format (player/enemy/admin) for guild and tell
    // chat, while only the message body takes the guild/tell color.
    internal static bool TryGetDisplayName(string name, string recipient, bool toMe, out string displayName, out uint color) {
        color = PlayerColor;
        displayName = name ?? string.Empty;

        switch (displayName) {
            case ServerChatName:
            case ClientChatName:
            case ErrorChatName:
            case HelpChatName:
                displayName = string.Empty;
                return false;
        }

        if (displayName.StartsWith(EnemyNameChar)) {
            color = EnemyColor;
            displayName = displayName.Substring(1);
        }

        if (displayName.StartsWith(AdminNameChar)) {
            color = AdminColor;
            displayName = displayName.Substring(1);
        }

        if (recipient != GuildChatName && recipient != string.Empty && !toMe) {
            displayName = recipient;
        }

        return true;
    }

    internal static uint GetMessageColor(string name, string recipient) {
        var color = DefaultColor;

        var resolved = name ?? string.Empty;

        color = resolved switch {
            ServerChatName => ServerColor,
            ClientChatName => ClientColor,
            ErrorChatName => ErrorColor,
            HelpChatName => HelpColor,
            _ => color
        };

        if (resolved.StartsWith(AdminNameChar)) {
            color = AdminColor;
        }

        if (recipient == GuildChatName) {
            color = GuildColor;
        } else if (recipient != string.Empty) {
            color = TellColor;
        }

        return color;
    }

    private static SimpleText CreateText(string text, uint color, int maxWidth = -1) {
        return new SimpleText(new TextConfig {
            Text = text,
            FontSize = FontSize,
            FontType = FontType.Bold,
            Color = color,
            OutlineThickness = 3,
            MaxWidth = maxWidth
        });
    }
    
}