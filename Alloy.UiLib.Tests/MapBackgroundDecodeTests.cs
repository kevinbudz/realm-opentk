using AlloyClient.Screens.Components;

namespace Alloy.UiLib.Tests;

internal static class MapBackgroundDecodeTests {
    public static void Run() {
        var map = MapBackground.DecodeEmbeddedMap();

        Equal(128, map.Width);
        Equal(128, map.Height);
        Equal(128 * 128, map.Indices.Length);

        foreach (var index in map.Indices) {
            if (index < 0 || index >= map.Entries.Length) {
                throw new Exception($"tile references missing dictionary entry {index}");
            }
        }

        // Spot-check the first entries against the Flash source data.
        Equal("Dark Grass", map.Entries[0].Ground);
        Equal("Dark Grass", map.Entries[1].Ground);
        Equal(1, map.Entries[1].Objects.Length);

        var grounds = 0;
        var objects = 0;
        foreach (var entry in map.Entries) {
            if (entry.Ground != null) {
                grounds++;
            }

            objects += entry.Objects.Length;
        }

        if (grounds == 0 || objects == 0) {
            throw new Exception("background map has no tiles or objects");
        }

        Console.WriteLine($"background map: {map.Width}x{map.Height}, {map.Entries.Length} entries, {objects} objects");
    }

    private static void Equal<T>(T expected, T actual) {
        if (!EqualityComparer<T>.Default.Equals(expected, actual)) {
            throw new Exception($"expected {expected}, got {actual}");
        }
    }
}
