using Alloy.UiLib.Core;
using Alloy.UiLib.Extra;

namespace AlloyClient.Ui.Flash;

/// <summary>
/// Immutable text parameters shared by Flash-parity Alloy components.
/// </summary>
/// <remarks>
/// Font sizes are logical Flash design units.  Colors are RGB values in the same
/// packed hexadecimal form used by the Flash source and by Alloy's text config;
/// alpha is supplied by the component when it renders the style.
/// </remarks>
public readonly record struct FlashTextStyle(
    FontType FontType,
    float FontSize,
    uint Color,
    float OutlineThickness = 0,
    uint OutlineColor = 0,
    DropShadowFilter DropShadow = null);

/// <summary>Shared filter values from the original ActionScript text call sites.</summary>
public static class FlashTextFilters {
    public static DropShadowFilter Default { get; } = new(distance: 0, angle: 0);
    public static DropShadowFilter StrongOutline { get; } = new(distance: 0, angle: 0, strength: 2);
    public static DropShadowFilter Soft { get; } = new(distance: 0, angle: 0, alpha: 0.5f, blurX: 12, blurY: 12);
}

/// <summary>
/// Color tokens copied from the Flash client's shared text formats.
/// </summary>
/// <remarks>
/// The chat message colors are the values assigned by
/// <c>com.company.assembleegameclient.ui.ElementFormats</c>.  The neutral and
/// tooltip colors come from the Flash <c>SimpleText</c> and <c>TextToolTip</c>
/// call sites.
/// </remarks>
public static class FlashUiTokens {
    /// <summary>Opaque white used for primary text.</summary>
    public const uint TextPrimary = 0xFFFFFF;

    /// <summary>Muted gray used for descriptions and tooltip body text.</summary>
    public const uint TextSecondary = 0xB3B3B3;

    /// <summary>Dark gray used for separators and text on light buttons.</summary>
    public const uint TextSeparator = 0x363636;

    /// <summary>Server chat text color.</summary>
    public const uint ChatServer = 0xFFFF00;

    /// <summary>Client/system chat text color.</summary>
    public const uint ChatClient = 0x0000FF;

    /// <summary>Help chat text color.</summary>
    public const uint ChatHelp = 0xFF5B05;

    /// <summary>Error chat text color.</summary>
    public const uint ChatError = 0xFF0000;

    /// <summary>Administrator chat text color.</summary>
    public const uint ChatAdmin = 0xFFFF00;

    /// <summary>Enemy-name chat text color.</summary>
    public const uint ChatEnemy = 0xFFA800;

    /// <summary>Player-name chat text color.</summary>
    public const uint ChatPlayer = 0x00FF00;

    /// <summary>Tell chat text color.</summary>
    public const uint ChatTell = 0x00F0FF;

    /// <summary>Guild chat text color.</summary>
    public const uint ChatGuild = 0xA6FF5D;
}

/// <summary>
/// Named Flash text styles for common migrated surfaces.
/// </summary>
public static class FlashTextStyles {
    /// <summary>Normal chat text: the Flash shared format's bold 14-point white text.</summary>
    public static FlashTextStyle ChatNormal { get; } = new(FontType.Bold, 14, FlashUiTokens.TextPrimary);

    /// <summary>Server chat text.</summary>
    public static FlashTextStyle ChatServer { get; } = ChatNormal with { Color = FlashUiTokens.ChatServer };

    /// <summary>Client/system chat text.</summary>
    public static FlashTextStyle ChatClient { get; } = ChatNormal with { Color = FlashUiTokens.ChatClient };

    /// <summary>Help chat text.</summary>
    public static FlashTextStyle ChatHelp { get; } = ChatNormal with { Color = FlashUiTokens.ChatHelp };

    /// <summary>Error chat text.</summary>
    public static FlashTextStyle ChatError { get; } = ChatNormal with { Color = FlashUiTokens.ChatError };

    /// <summary>Administrator chat text.</summary>
    public static FlashTextStyle ChatAdmin { get; } = ChatNormal with { Color = FlashUiTokens.ChatAdmin };

    /// <summary>Enemy-name chat text.</summary>
    public static FlashTextStyle ChatEnemy { get; } = ChatNormal with { Color = FlashUiTokens.ChatEnemy };

    /// <summary>Player-name chat text.</summary>
    public static FlashTextStyle ChatPlayer { get; } = ChatNormal with { Color = FlashUiTokens.ChatPlayer };

    /// <summary>Separator text.</summary>
    public static FlashTextStyle ChatSeparator { get; } = ChatNormal with { Color = FlashUiTokens.TextSeparator };

    /// <summary>Tell chat text.</summary>
    public static FlashTextStyle ChatTell { get; } = ChatNormal with { Color = FlashUiTokens.ChatTell };

    /// <summary>Guild chat text.</summary>
    public static FlashTextStyle ChatGuild { get; } = ChatNormal with { Color = FlashUiTokens.ChatGuild };

    /// <summary>HUD status-bar labels and values (bold 14-point white text).</summary>
    public static FlashTextStyle HudValue { get; } = new(FontType.Bold, 14, FlashUiTokens.TextPrimary, DropShadow: FlashTextFilters.Default);

    /// <summary>Title-screen primary action text.</summary>
    public static FlashTextStyle TitlePrimaryAction { get; } = new(FontType.Bold, 36, FlashUiTokens.TextPrimary, DropShadow: FlashTextFilters.Soft);

    /// <summary>Title-screen secondary action text.</summary>
    public static FlashTextStyle TitleAction { get; } = new(FontType.Bold, 22, FlashUiTokens.TextPrimary, DropShadow: FlashTextFilters.Soft);

    /// <summary>Tooltip title text.</summary>
    public static FlashTextStyle TooltipTitle { get; } = new(FontType.Bold, 20, FlashUiTokens.TextPrimary);

    /// <summary>Tooltip body text.</summary>
    public static FlashTextStyle TooltipBody { get; } = new(FontType.Normal, 14, FlashUiTokens.TextSecondary);
}
