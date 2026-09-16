using System;

namespace AlloyClient.Ui.Flash;

/// <summary>
/// Coordinates in the Flash client's logical 800x600 design space.
/// </summary>
/// <remarks>
/// Design coordinates are independent of both the window's client coordinates and
/// the framebuffer's physical pixel coordinates.  They are the units used by the
/// AS3 menu and HUD layout constants.
/// </remarks>
public readonly record struct FlashDesignPoint(float X, float Y);

/// <summary>
/// Coordinates in the window's client coordinate system, before any framebuffer
/// pixel-density conversion.
/// </summary>
/// <remarks>
/// Pointer events from the window toolkit are expected to use this coordinate
/// system.  The origin is the top-left of the client area, as it is in Flash.
/// </remarks>
public readonly record struct FlashWindowPoint(float X, float Y);

/// <summary>
/// Coordinates in the physical framebuffer used by the renderer.
/// </summary>
/// <remarks>
/// A framebuffer point has the same top-left origin as a window point.  Its
/// values can differ when a logical window is rendered on a high-DPI display.
/// </remarks>
public readonly record struct FlashFramebufferPoint(float X, float Y);

/// <summary>
/// The immutable mapping between Flash design space, a window client area, and
/// its physical framebuffer.
/// </summary>
/// <remarks>
/// Flash screens are authored at 800x600.  Their content is scaled uniformly by
/// the window height (<c>windowHeight / 600</c>) and then centered in the window.
/// This deliberately uses height-based scaling rather than fitting the design
/// rectangle to both axes; a narrow window may therefore crop horizontal design
/// content just as the Flash client does.
/// </remarks>
public readonly struct FlashViewport : IEquatable<FlashViewport> {
    /// <summary>Gets the logical client width of the window.</summary>
    public int WindowWidth { get; }

    /// <summary>Gets the logical client height of the window.</summary>
    public int WindowHeight { get; }

    /// <summary>Gets the physical framebuffer width.</summary>
    public int FramebufferWidth { get; }

    /// <summary>Gets the physical framebuffer height.</summary>
    public int FramebufferHeight { get; }

    /// <summary>Gets the height-based scale from design units to window units.</summary>
    public float Scale { get; }

    /// <summary>Gets the centered content's left offset in window units.</summary>
    public float ContentOffsetX { get; }

    /// <summary>Gets the centered content's top offset in window units.</summary>
    public float ContentOffsetY { get; }

    /// <summary>
    /// Creates a viewport using the same dimensions for the logical window and
    /// framebuffer.
    /// </summary>
    /// <param name="windowWidth">Logical client width in pixels.</param>
    /// <param name="windowHeight">Logical client height in pixels.</param>
    public FlashViewport(int windowWidth, int windowHeight)
        : this(windowWidth, windowHeight, windowWidth, windowHeight) {
    }

    /// <summary>
    /// Creates a viewport and records the logical-to-physical framebuffer sizes.
    /// </summary>
    /// <param name="windowWidth">Logical client width in pixels.</param>
    /// <param name="windowHeight">Logical client height in pixels.</param>
    /// <param name="framebufferWidth">Physical framebuffer width in pixels.</param>
    /// <param name="framebufferHeight">Physical framebuffer height in pixels.</param>
    public FlashViewport(int windowWidth, int windowHeight, int framebufferWidth, int framebufferHeight) {
        if (windowWidth <= 0) {
            throw new ArgumentOutOfRangeException(nameof(windowWidth), windowWidth, "Window width must be positive.");
        }

        if (windowHeight <= 0) {
            throw new ArgumentOutOfRangeException(nameof(windowHeight), windowHeight, "Window height must be positive.");
        }

        if (framebufferWidth <= 0) {
            throw new ArgumentOutOfRangeException(nameof(framebufferWidth), framebufferWidth, "Framebuffer width must be positive.");
        }

        if (framebufferHeight <= 0) {
            throw new ArgumentOutOfRangeException(nameof(framebufferHeight), framebufferHeight, "Framebuffer height must be positive.");
        }

        WindowWidth = windowWidth;
        WindowHeight = windowHeight;
        FramebufferWidth = framebufferWidth;
        FramebufferHeight = framebufferHeight;
        Scale = FlashLayout.ScaleForHeight(windowHeight);
        ContentOffsetX = FlashLayout.ContentOffsetX(windowWidth, Scale);
        ContentOffsetY = FlashLayout.ContentOffsetY(windowHeight, Scale);
    }

    /// <summary>Maps a design-space point into window client coordinates.</summary>
    public FlashWindowPoint DesignToWindow(FlashDesignPoint point) {
        return new FlashWindowPoint(
            ContentOffsetX + point.X * Scale,
            ContentOffsetY + point.Y * Scale);
    }

    /// <summary>Maps a window client point back into Flash design coordinates.</summary>
    public FlashDesignPoint WindowToDesign(FlashWindowPoint point) {
        return new FlashDesignPoint(
            (point.X - ContentOffsetX) / Scale,
            (point.Y - ContentOffsetY) / Scale);
    }

    /// <summary>Maps a window client point into physical framebuffer coordinates.</summary>
    public FlashFramebufferPoint WindowToFramebuffer(FlashWindowPoint point) {
        return new FlashFramebufferPoint(
            point.X * FramebufferWidth / WindowWidth,
            point.Y * FramebufferHeight / WindowHeight);
    }

    /// <summary>Maps a physical framebuffer point into window client coordinates.</summary>
    public FlashWindowPoint FramebufferToWindow(FlashFramebufferPoint point) {
        return new FlashWindowPoint(
            point.X * WindowWidth / FramebufferWidth,
            point.Y * WindowHeight / FramebufferHeight);
    }

    /// <summary>Maps a design-space point directly into framebuffer coordinates.</summary>
    public FlashFramebufferPoint DesignToFramebuffer(FlashDesignPoint point) {
        return WindowToFramebuffer(DesignToWindow(point));
    }

    /// <summary>Maps a framebuffer point directly into design coordinates.</summary>
    public FlashDesignPoint FramebufferToDesign(FlashFramebufferPoint point) {
        return WindowToDesign(FramebufferToWindow(point));
    }

    /// <inheritdoc />
    public bool Equals(FlashViewport other) {
        return WindowWidth == other.WindowWidth
            && WindowHeight == other.WindowHeight
            && FramebufferWidth == other.FramebufferWidth
            && FramebufferHeight == other.FramebufferHeight;
    }

    /// <inheritdoc />
    public override bool Equals(object obj) => obj is FlashViewport other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(WindowWidth, WindowHeight, FramebufferWidth, FramebufferHeight);

    /// <summary>Compares two viewport mappings.</summary>
    public static bool operator ==(FlashViewport left, FlashViewport right) => left.Equals(right);

    /// <summary>Compares two viewport mappings.</summary>
    public static bool operator !=(FlashViewport left, FlashViewport right) => !left.Equals(right);
}

/// <summary>
/// Stateless layout math shared by Alloy surfaces that are migrating from Flash.
/// </summary>
public static class FlashLayout {
    /// <summary>The canonical Flash design width in logical units.</summary>
    public const int DesignWidth = 800;

    /// <summary>The canonical Flash design height in logical units.</summary>
    public const int DesignHeight = 600;

    /// <summary>The native client width; equal to the design width by contract.</summary>
    public const int NativeWidth = DesignWidth;

    /// <summary>The native client height; equal to the design height by contract.</summary>
    public const int NativeHeight = DesignHeight;

    /// <summary>Returns the Flash height-based scale for a window height.</summary>
    /// <remarks>Non-positive heights use Flash's neutral fallback scale of 1.</remarks>
    public static float ScaleForHeight(int windowHeight) {
        return windowHeight <= 0 ? 1f : windowHeight / (float)DesignHeight;
    }

    /// <summary>Returns the horizontal offset that centers scaled design content.</summary>
    public static float ContentOffsetX(int windowWidth, float scale) {
        return (windowWidth - DesignWidth * scale) / 2f;
    }

    /// <summary>Returns the vertical offset that centers scaled design content.</summary>
    public static float ContentOffsetY(int windowHeight, float scale) {
        return (windowHeight - DesignHeight * scale) / 2f;
    }

    /// <summary>Returns the design-space X coordinate that centers an object.</summary>
    public static float CenterX(float objectWidth, float areaWidth = DesignWidth) {
        return (areaWidth - objectWidth) / 2f;
    }

    /// <summary>Returns the design-space Y coordinate that centers an object.</summary>
    public static float CenterY(float objectHeight, float areaHeight = DesignHeight) {
        return (areaHeight - objectHeight) / 2f;
    }

    /// <summary>Returns the design-space X coordinate that right-aligns an object.</summary>
    public static float RightX(float objectWidth, float margin = 0f, float areaWidth = DesignWidth) {
        return areaWidth - margin - objectWidth;
    }

    /// <summary>Creates a viewport where logical window and framebuffer sizes match.</summary>
    public static FlashViewport CreateViewport(int windowWidth, int windowHeight) {
        return new FlashViewport(windowWidth, windowHeight);
    }

    /// <summary>Creates a viewport with separate logical window and framebuffer sizes.</summary>
    public static FlashViewport CreateViewport(int windowWidth, int windowHeight, int framebufferWidth, int framebufferHeight) {
        return new FlashViewport(windowWidth, windowHeight, framebufferWidth, framebufferHeight);
    }
}
