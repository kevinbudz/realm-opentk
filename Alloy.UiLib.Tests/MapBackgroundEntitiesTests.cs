using System.Xml.Linq;
using AlloyClient.Assets;
using AlloyClient.Assets.Libraries;
using AlloyClient.Assets.XmlStructs;
using AlloyClient.Game;
using AlloyClient.Game.Objects;
using AlloyClient.Rendering.Types;
using AlloyClient.Screens.Components;
using Alloy.Engine;
using OpenTK.Mathematics;

namespace Alloy.UiLib.Tests;

// Background decor is static: it must be added with the right render type,
// stay pinned to its spawn tile (movement interpolation drags unpinned
// entities to the origin), and be visible under a camera aimed at it.
internal static class MapBackgroundEntitiesTests {
    public static void Run() {
        Map.Reset();

        const ushort fakeType = 0x9999;
        var xml = XElement.Parse(
            "<Object type=\"0x9999\" id=\"FakeWall\"><Class>Wall</Class><Static/><OccupySquare/></Object>");
        ObjectLibrary.IdToObjectType["FakeWall"] = fakeType;
        ObjectLibrary.TypeToObjectProps[fakeType] = new ObjectProperties(xml);
        ObjectLibrary.TypeToTextureData[fakeType] = new TextureData(xml);

        MapBackground.AddBackgroundObject("FakeWall", 5, 5);

        Entity? wall = null;
        foreach (var entity in Map.Entities.Values) {
            wall = entity;
        }

        if (wall == null) {
            throw new Exception("background wall was not added to the map");
        }

        if (wall.RenderBaseType is not TypeWall) {
            throw new Exception($"expected TypeWall, got {wall.RenderBaseType?.GetType().Name}");
        }

        Equal(new Vector2(5.5f, 5.5f), wall.Position);

        var camera = Camera.Update(new Vector2(5.5f, 5.5f), new Vector3i(800, 600, 0), 7f * MathF.PI / 4f, 1f);
        var gameTime = new GameTime(1000, 1000d / 60d);
        for (var i = 0; i < 60; i++) {
            Map.Update(gameTime, camera);
        }

        Equal(new Vector2(5.5f, 5.5f), wall.Position);

        if (!wall.RenderBaseType.Visible) {
            throw new Exception("background wall is culled under its own camera");
        }

        Map.Reset();
        Console.WriteLine("background wall: placed, pinned and visible");
    }

    private static void Equal<T>(T expected, T actual) {
        if (!EqualityComparer<T>.Default.Equals(expected, actual)) {
            throw new Exception($"expected {expected}, got {actual}");
        }
    }
}
