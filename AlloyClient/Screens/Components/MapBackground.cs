using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using AlloyClient.Assets.Libraries;
using AlloyClient.Game;
using AlloyClient.Game.Objects;
using AlloyClient.Logging;
using AlloyClient.Networking.Structs.DataObjects;
using AlloyClient.Rendering;
using Alloy.Engine;
using Microsoft.Extensions.Logging;
using OpenTK.Mathematics;

namespace AlloyClient.Screens.Components;

/// <summary>
/// Live scrolling map backdrop for menu screens, ported from the Flash
/// <c>MapBackground</c> movieclip (not a <c>Sprite</c>: the world is drawn in
/// the screen's world pass, ahead of the UI stage, like <c>GameScreen</c>).
///
/// An embedded background map pans slowly past a fixed-angle camera at one
/// tile per second, wrapping seamlessly. The map is written double-wide so
/// the scroll can wrap, and the camera viewport tracks the real window size
/// floored at the 800x600 design space. The zoom scales with window height
/// against the 600px baseline so taller windows magnify the map instead of
/// revealing more of it (and its edges).
///
/// The decoded map is shared across all menu screens (matching the Flash
/// statics), so navigating between menus keeps the scroll position. It only
/// reloads when the shared <c>Map</c> holds something else (e.g. returning
/// from a game). Entering a game is unaffected: <c>MapInfo</c> resets the
/// shared map before initializing the server map.
/// </summary>
public sealed class MapBackground {
    private const int Border = 10;
    private const float Angle = 7f * MathF.PI / 4f;

    // Flash uses the non-perspective camera path with a fixed scale of 50,
    // i.e. one tile covers 50 screen pixels at the 800x600 design size.
    // Alloy's orthographic range spans twice the viewport width, halving
    // the base zoom of 100 to the same 50 pixels per tile, so a zoom of 1
    // reproduces the Flash framing. Taller windows zoom in proportionally
    // (height / 600) so they show the same vertical slice of the map
    // instead of revealing its edges.
    internal float Zoom => _viewHeight / (float)DesignHeight;

    private const int DesignWidth = 800;
    private const int DesignHeight = 600;

    private const string EmbeddedResourceName = "AlloyClient.Screens.Components.MapBackground.dat";

    private readonly static ILogger Logger = ILogger.CreateLogger(nameof(MapBackground));

    private static bool _loaded;
    private static bool _loadFailed;
    private static int _mapWidth;
    private static int _mapHeight;
    private static float _xVal = Border;
    private static float _yVal;
    private static int _nextFakeObjectId = -1;

    private int _viewWidth = DesignWidth;
    private int _viewHeight = DesignHeight;
    private Camera _camera;
    private bool _hasCamera;

    public bool IsActive => _loaded && _hasCamera && MapMatchesBackground();

    public void Resize(int width, int height) {
        _viewWidth = Math.Max(DesignWidth, width);
        _viewHeight = Math.Max(DesignHeight, height);
    }

    public void Update(GameTime gameTime) {
        if (!EnsureLoaded()) {
            return;
        }

        _xVal += (float)gameTime.ElapsedMs * 0.001f;
        if (_xVal > _mapWidth + Border) {
            _xVal -= _mapWidth;
        }

        _camera = Camera.Update(new Vector2(_xVal, _yVal),
            new Vector3i(_viewWidth, _viewHeight, 0), Angle, Zoom);
        _hasCamera = true;

        Map.Update(gameTime, _camera);
    }

    public void Draw(GameTime gameTime) {
        if (!IsActive) {
            return;
        }

        Render.SetShaderParams(gameTime, _camera);
        Map.Draw(gameTime, _camera);
    }

    private static bool MapMatchesBackground() =>
        Map.Width == _mapWidth + 2 * Border &&
        Map.Height == _mapHeight &&
        Map.Name == "Background Map";

    private static bool EnsureLoaded() {
        if (_loadFailed) {
            return false;
        }

        if (_loaded) {
            return MapMatchesBackground() || Reload();
        }

        if (!LibrariesReady()) {
            return false;
        }

        return Reload();
    }

    private static bool LibrariesReady() =>
        GroundLibrary.TypeToGroundProps.Count > 0 &&
        GroundLibrary.IdToTileType.Count > 0 &&
        ObjectLibrary.IdToObjectType.Count > 0 &&
        ObjectLibrary.TypeToObjectProps.Count > 0;

    private static bool Reload() {
        try {
            Load();
            return true;
        } catch (Exception ex) {
            // Never break the menu over its backdrop; the static splash
            // texture stays visible and this is retried only via a restart.
            _loadFailed = true;
            Logger.Log(LogLevel.Warning, ex, "[MapBackground] Failed to load the background map.");
            return false;
        }
    }

    private static void Load() {
        var decoded = DecodeEmbeddedMap();

        _mapWidth = decoded.Width;
        _mapHeight = decoded.Height;

        Map.Reset();
        Map.InitMap(_mapWidth + 2 * Border, _mapHeight,
            "Background Map", "Background Map", 0, 0, 0, false, false);

        WriteMap(decoded, 0, 0);
        WriteMap(decoded, _mapWidth, 0);

        _xVal = Border;
        _yVal = Border + Random.Shared.NextSingle() * (_mapHeight - 2 * Border);
        _loaded = true;
    }

    private static void WriteMap(DecodedBackgroundMap decoded, int offsetX, int offsetY) {
        for (var y = 0; y < decoded.Height; y++) {
            for (var x = 0; x < decoded.Width; x++) {
                var tx = x + offsetX;
                var ty = y + offsetY;

                // The double-wide write overflows the map on purpose; the
                // Flash decoder clips it, keeping only the overlapping strip
                // that makes the horizontal wrap seamless.
                if (tx < 0 || tx >= Map.Width || ty < 0 || ty >= Map.Height) {
                    continue;
                }

                var entry = decoded.Entries[decoded.Indices[y * decoded.Width + x]];

                if (entry.Ground != null &&
                    GroundLibrary.IdToTileType.TryGetValue(entry.Ground, out var groundType)) {
                    Map.SetTileData(tx, ty, groundType);
                }

                foreach (var objId in entry.Objects) {
                    AddBackgroundObject(objId, tx, ty);
                }
            }
        }
    }

    internal static void AddBackgroundObject(string objId, int x, int y) {
        if (!ObjectLibrary.IdToObjectType.TryGetValue(objId, out var objectType) ||
            !ObjectLibrary.TypeToObjectProps.TryGetValue(objectType, out var props)) {
            return;
        }

        Entity entity = props.IsPlayer ? new Player() : new Entity();
        entity.Properties = props;
        entity.SetObjectId(_nextFakeObjectId--);
        entity.SetType(objectType);

        var position = new Position(x + 0.5f, y + 0.5f);
        Map.AddEntity(entity, position);
        entity.SetPos(position.X, position.Y);

        // Static decor never moves: pin the tick state to the spawn position.
        // Otherwise Entity.Update's movement interpolation drags every
        // background object toward the origin, off-camera.
        entity.TickPosition = entity.Position;
        entity.PositionAtTick = entity.Position;
        entity.MovementVector = Vector2.Zero;
    }

    public static DecodedBackgroundMap DecodeEmbeddedMap() {
        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream(EmbeddedResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{EmbeddedResourceName}' not found.");
        using var reader = new StreamReader(stream);
        return DecodeMapJson(reader.ReadToEnd());
    }

    /// <summary>
    /// Decodes the Flash background-map JSON: big-endian int16 dictionary
    /// indices over zlib-compressed base64 tile data, like
    /// <c>MapDecoder.writeMapInternal</c>.
    /// </summary>
    public static DecodedBackgroundMap DecodeMapJson(string encodedMap) {
        using var document = JsonDocument.Parse(encodedMap);
        var root = document.RootElement;

        var width = root.GetProperty("width").GetInt32();
        var height = root.GetProperty("height").GetInt32();

        var entries = new List<BackgroundMapEntry>();
        foreach (var jsonEntry in root.GetProperty("dict").EnumerateArray()) {
            string ground = null;
            if (jsonEntry.TryGetProperty("ground", out var jsonGround)) {
                ground = jsonGround.GetString();
            }

            var objects = new List<string>();
            if (jsonEntry.TryGetProperty("objs", out var jsonObjs)) {
                foreach (var jsonObj in jsonObjs.EnumerateArray()) {
                    objects.Add(jsonObj.GetProperty("id").GetString());
                }
            }

            entries.Add(new BackgroundMapEntry(ground, objects.ToArray()));
        }

        var compressed = Convert.FromBase64String(root.GetProperty("data").GetString());
        using var compressedStream = new MemoryStream(compressed);
        using var zlib = new ZLibStream(compressedStream, CompressionMode.Decompress);
        using var rawStream = new MemoryStream();
        zlib.CopyTo(rawStream);

        var raw = rawStream.ToArray();
        if (raw.Length != width * height * 2) {
            throw new InvalidDataException(
                $"Background map data is {raw.Length} bytes, expected {width * height * 2}.");
        }

        var indices = new short[width * height];
        for (var i = 0; i < indices.Length; i++) {
            indices[i] = BinaryPrimitives.ReadInt16BigEndian(raw.AsSpan(i * 2, 2));
            if (indices[i] < 0 || indices[i] >= entries.Count) {
                throw new InvalidDataException(
                    $"Background map tile {i} references dictionary entry {indices[i]} of {entries.Count}.");
            }
        }

        return new DecodedBackgroundMap(width, height, indices, entries.ToArray());
    }

    public sealed record DecodedBackgroundMap(
        int Width, int Height, short[] Indices, BackgroundMapEntry[] Entries);

    public sealed record BackgroundMapEntry(string Ground, string[] Objects);
}
