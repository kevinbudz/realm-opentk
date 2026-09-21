using AlloyClient.Game.Components.Hud.Chat;

namespace Alloy.UiLib.Tests;

// Flash parity for the HUD chat lines: TextBox.as shows 10 lines scrolled 3
// at a time from x=2, while TextBoxLine.as with ElementFormats.as renders
// bold 14pt text on a 16px minimum line with a 20px wrap indent, a gray
// separator after the name, the sender-derived name color for guild/tell
// chat, and a rank icon everywhere except outgoing tells.
internal static class ChatParityTests {
    public static void Run() {
        LineConstants();
        StarVisibility();
        PmVisibility();
        DisplayName();
        MessageColor();
    }

    // TextBox.as MAX_LINES/LINE_SCROLL/INDENT/MIN_LINE_HEIGHT and the
    // ElementFormats 14pt size with the sepFormat 0x363636 separator.
    private static void LineConstants() {
        Equal(10, ChatBox.MaxLines);
        Equal(3, ChatBox.LineScroll);
        Equal(14, ChatBoxLine.FontSize);
        Equal(16, ChatBoxLine.MinLineHeight);
        Equal(20, ChatBoxLine.Indent);
        Equal(0x363636u, ChatBoxLine.SeparatorColor);
    }

    // TextBoxLine.getTextBlock clears the rank icon only for outgoing tells;
    // negative stars never show one and guild chat keeps it.
    private static void StarVisibility() {
        Equal(false, ChatBoxLine.ShouldShowStar(-1, "", false));
        Equal(true, ChatBoxLine.ShouldShowStar(0, "", false));
        Equal(false, ChatBoxLine.ShouldShowStar(5, "Bob", false));
        Equal(true, ChatBoxLine.ShouldShowStar(5, "Bob", true));
        Equal(true, ChatBoxLine.ShouldShowStar(5, "*Guild*", false));
        Equal(true, ChatBoxLine.ShouldShowStar(5, "*Guild*", true));
    }

    // The white "To: " prefix appears only on outgoing tells.
    private static void PmVisibility() {
        Equal(true, ChatBoxLine.ShouldShowPm("Bob", false));
        Equal(false, ChatBoxLine.ShouldShowPm("Bob", true));
        Equal(false, ChatBoxLine.ShouldShowPm("*Guild*", false));
        Equal(false, ChatBoxLine.ShouldShowPm("", false));
    }

    private static void DisplayName() {
        // System senders show no name element.
        foreach (var system in new[] { "", "*Client*", "*Error*", "*Help*" }) {
            Equal(false, ChatBoxLine.TryGetDisplayName(system, "", false, out _, out _));
        }

        // Plain sender: green name.
        Equal(true, ChatBoxLine.TryGetDisplayName("Bob", "", false, out var name, out var color));
        Equal("Bob", name);
        Equal(0x00FF00u, color);

        // Enemy and admin prefixes are stripped with their own colors.
        Equal(true, ChatBoxLine.TryGetDisplayName("#Oryx", "", false, out name, out color));
        Equal("Oryx", name);
        Equal(0xFFA800u, color);

        Equal(true, ChatBoxLine.TryGetDisplayName("@Admin", "", false, out name, out color));
        Equal("Admin", name);
        Equal(0xFFFF00u, color);

        // Guild chat keeps the sender-derived name color, not the guild text color.
        Equal(true, ChatBoxLine.TryGetDisplayName("Bob", "*Guild*", false, out name, out color));
        Equal("Bob", name);
        Equal(0x00FF00u, color);

        // Outgoing tells show the recipient in the sender-derived color.
        Equal(true, ChatBoxLine.TryGetDisplayName("Alice", "Bob", false, out name, out color));
        Equal("Bob", name);
        Equal(0x00FF00u, color);

        // Incoming tells keep the sender.
        Equal(true, ChatBoxLine.TryGetDisplayName("Alice", "Me", true, out name, out _));
        Equal("Alice", name);
    }

    private static void MessageColor() {
        Equal(0xFFFF00u, ChatBoxLine.GetMessageColor("", ""));
        Equal(0x0000FFu, ChatBoxLine.GetMessageColor("*Client*", ""));
        Equal(0xFF0000u, ChatBoxLine.GetMessageColor("*Error*", ""));
        Equal(0xFF5B05u, ChatBoxLine.GetMessageColor("*Help*", ""));
        Equal(0xFFFF00u, ChatBoxLine.GetMessageColor("@Admin", ""));
        Equal(0xFFFFFFu, ChatBoxLine.GetMessageColor("Bob", ""));
        Equal(0xFFFFFFu, ChatBoxLine.GetMessageColor("#Oryx", ""));
        Equal(0xA6FF5Du, ChatBoxLine.GetMessageColor("Bob", "*Guild*"));
        Equal(0x00F0FFu, ChatBoxLine.GetMessageColor("Bob", "Alice"));
        Equal(0x00F0FFu, ChatBoxLine.GetMessageColor("Bob", "Me"));
        Equal(0x00F0FFu, ChatBoxLine.GetMessageColor("@Admin", "Bob"));
    }

    private static void Equal<T>(T expected, T actual) {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new Exception($"expected {expected}, got {actual}");
    }
}
