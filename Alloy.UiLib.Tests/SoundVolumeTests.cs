using AlloyClient;

namespace Alloy.UiLib.Tests;

// The Sound tab is sliders-only: the volume values are the single source of
// truth (0 is muted), so the applied volume must match the slider position
// directly with no hidden mute gate silencing it.
internal static class SoundVolumeTests {
    internal static void Run() {
        VolumesApplyDirectly();
        VolumesClampToUnitRange();
    }

    private static void VolumesApplyDirectly() {
        try {
            Settings.ResetToDefault();

            Settings.SetMasterVolume(0.7f);
            Equal(0.7f, Settings.MasterVolume.Value, 1e-6f);
            Equal(0.7f, Settings.GetMasterVolume(), 1e-6f);

            Settings.SetMusicVolume(0.3f);
            Equal(0.3f, Settings.MusicVolume.Value, 1e-6f);
            Equal(0.3f, Settings.GetMusicVolume(), 1e-6f);

            Settings.SetSfxVolume(0.9f);
            Equal(0.9f, Settings.SfxVolume.Value, 1e-6f);
            Equal(0.9f, Settings.GetSfxVolume(), 1e-6f);

            Settings.SetMasterVolume(0f);
            Equal(0f, Settings.GetMasterVolume(), 1e-6f);
        } finally {
            Settings.ResetToDefault();
        }
    }

    private static void VolumesClampToUnitRange() {
        try {
            Settings.ResetToDefault();

            Settings.SetMasterVolume(2f);
            Equal(1f, Settings.MasterVolume.Value, 1e-6f);

            Settings.SetMusicVolume(-1f);
            Equal(0f, Settings.MusicVolume.Value, 1e-6f);

            Settings.SetSfxVolume(0.5f);
            Equal(0.5f, Settings.SfxVolume.Value, 1e-6f);
        } finally {
            Settings.ResetToDefault();
        }
    }

    private static void Equal<T>(T expected, T actual) {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new Exception($"expected {expected}, got {actual}");
    }

    private static void Equal(float expected, float actual, float tolerance) {
        if (Math.Abs(expected - actual) > tolerance)
            throw new Exception($"expected {expected}, got {actual}");
    }
}
