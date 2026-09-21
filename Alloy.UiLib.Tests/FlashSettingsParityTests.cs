using AlloyClient;
using OpenTK.Platform;

namespace Alloy.UiLib.Tests;

// Flash parity for the ported gameplay settings: defaults must match
// realm-client Parameters.setDefaults, the ally filters must mirror
// GameServerConnection's allyShots/allyDamage/allyNotifs gates, and the new
// settings must survive a serialize/deserialize round trip.
internal static class FlashSettingsParityTests {
    internal static void Run() {
        FlashDefaults();
        AllyShotFilter();
        AllyDamageFilter();
        AllyNotificationFilter();
        AllyInfoMasterGatesSubs();
        PlayerAlphaFilter();
        SettingsRoundTrip();
    }

    private static void FlashDefaults() {
        try {
            Settings.ResetToDefault();
            Equal(ParticleMode.On, Settings.EyeCandyParticles.Value);
            Equal(true, Settings.AllyInfo.Value);
            Equal(true, Settings.AllyShots.Value);
            Equal(true, Settings.AllyDamage.Value);
            Equal(true, Settings.AllyNotifs.Value);
            Equal(true, Settings.ProjectileOutline.Value);
            Equal(true, Settings.DrawShadows.Value);
            Equal(true, Settings.TextBubbles.Value);
            Equal(true, Settings.ShowQuestPortraits.Value);
            Equal(true, Settings.ShowGuildInvitePopup.Value);
            Equal(true, Settings.ShowTradePopup.Value);
            Equal(true, Settings.ShowTierTag.Value);
            Equal(false, Settings.PlayerAlpha.Value);
            Equal(1f, Settings.PlayerAlphaValue.Value, 1e-6f);
            Equal("4", Settings.Cursor.Value);
            Equal(7 * MathF.PI / 4, Settings.CameraAngle.Value, 1e-6f);
            Equal(7 * MathF.PI / 4, Settings.DefaultCameraAngle.Value, 1e-6f);
            Equal(Scancode.Equals, Settings.MiniMapZoomIn.Key);
            Equal(Scancode.Dash, Settings.MiniMapZoomOut.Key);
        } finally {
            Settings.ResetToDefault();
        }
    }

    private static void AllyShotFilter() {
        try {
            Settings.AllyShots.Set(true);
            Equal(true, Settings.ShouldShowAllyShot(false));
            Equal(true, Settings.ShouldShowAllyShot(true));

            Settings.AllyShots.Set(false);
            Equal(false, Settings.ShouldShowAllyShot(false));
            Equal(true, Settings.ShouldShowAllyShot(true));
        } finally {
            Settings.ResetToDefault();
        }
    }

    private static void AllyDamageFilter() {
        try {
            Settings.AllyDamage.Set(true);
            Equal(true, Settings.ShouldShowAllyDamage(8, 7));

            // Off: only the local player's own damage still shows.
            Settings.AllyDamage.Set(false);
            Equal(true, Settings.ShouldShowAllyDamage(7, 7));
            Equal(false, Settings.ShouldShowAllyDamage(8, 7));
        } finally {
            Settings.ResetToDefault();
        }
    }

    private static void AllyNotificationFilter() {
        try {
            Settings.AllyNotifs.Set(true);
            Equal(true, Settings.ShouldShowAllyNotification(true, 8, 7));

            // Off: non-player targets and the local player still show.
            Settings.AllyNotifs.Set(false);
            Equal(true, Settings.ShouldShowAllyNotification(false, 8, 7));
            Equal(true, Settings.ShouldShowAllyNotification(true, 7, 7));
            Equal(false, Settings.ShouldShowAllyNotification(true, 8, 7));
        } finally {
            Settings.ResetToDefault();
        }
    }

    private static void AllyInfoMasterGatesSubs() {
        try {
            Settings.AllyInfo.Set(true);
            Settings.AllyShots.Set(true);
            Settings.AllyDamage.Set(true);
            Settings.AllyNotifs.Set(true);
            Equal(true, Settings.ShouldShowAllyShot(false));
            Equal(true, Settings.ShouldShowAllyDamage(8, 7));
            Equal(true, Settings.ShouldShowAllyNotification(true, 8, 7));

            // Master off hides everything even with the sub-toggles on.
            Settings.AllyInfo.Set(false);
            Equal(false, Settings.ShouldShowAllyShot(false));
            Equal(false, Settings.ShouldShowAllyShot(true));
            Equal(false, Settings.ShouldShowAllyDamage(8, 7));
            Equal(false, Settings.ShouldShowAllyDamage(7, 7));
            Equal(false, Settings.ShouldShowAllyNotification(true, 8, 7));
            Equal(false, Settings.ShouldShowAllyNotification(false, 8, 7));

            // Sub-toggles still gate individually once the master is back on.
            Settings.AllyInfo.Set(true);
            Settings.AllyShots.Set(false);
            Equal(false, Settings.ShouldShowAllyShot(false));
        } finally {
            Settings.ResetToDefault();
        }
    }

    private static void PlayerAlphaFilter() {
        try {
            // Master off: everyone renders fully opaque regardless of the slider.
            Settings.PlayerAlpha.Set(false);
            Settings.PlayerAlphaValue.Set(0.25f);
            Equal(1f, Settings.GetOtherPlayerAlpha(true), 1e-6f);
            Equal(1f, Settings.GetOtherPlayerAlpha(false), 1e-6f);

            // Master on: the local player stays opaque, others use the slider.
            Settings.PlayerAlpha.Set(true);
            Settings.PlayerAlphaValue.Set(0.4f);
            Equal(1f, Settings.GetOtherPlayerAlpha(true), 1e-6f);
            Equal(0.4f, Settings.GetOtherPlayerAlpha(false), 1e-6f);

            // The slider clamps to the unit range.
            Settings.PlayerAlphaValue.Set(2f);
            Equal(1f, Settings.GetOtherPlayerAlpha(false), 1e-6f);
            Settings.PlayerAlphaValue.Set(-1f);
            Equal(0f, Settings.GetOtherPlayerAlpha(false), 1e-6f);
        } finally {
            Settings.ResetToDefault();
        }
    }

    private static void SettingsRoundTrip() {
        try {
            Settings.AllyInfo.Set(false);
            var info = Settings.AllyInfo.Serialize();
            Settings.AllyInfo.Set(true);
            Settings.AllyInfo.Deserialize(info);
            Equal(false, Settings.AllyInfo.Value);

            Settings.AllyShots.Set(false);
            var shots = Settings.AllyShots.Serialize();
            Settings.AllyShots.Set(true);
            Settings.AllyShots.Deserialize(shots);
            Equal(false, Settings.AllyShots.Value);

            Settings.Cursor.Set("7");
            var cursor = Settings.Cursor.Serialize();
            Settings.Cursor.Set("4");
            Settings.Cursor.Deserialize(cursor);
            Equal("7", Settings.Cursor.Value);

            Settings.EyeCandyParticles.Set(ParticleMode.Reduced);
            var particles = Settings.EyeCandyParticles.Serialize();
            Settings.EyeCandyParticles.Set(ParticleMode.On);
            Settings.EyeCandyParticles.Deserialize(particles);
            Equal(ParticleMode.Reduced, Settings.EyeCandyParticles.Value);

            Settings.PlayerAlpha.Set(true);
            var playerAlpha = Settings.PlayerAlpha.Serialize();
            Settings.PlayerAlpha.Set(false);
            Settings.PlayerAlpha.Deserialize(playerAlpha);
            Equal(true, Settings.PlayerAlpha.Value);

            Settings.PlayerAlphaValue.Set(0.35f);
            var playerAlphaValue = Settings.PlayerAlphaValue.Serialize();
            Settings.PlayerAlphaValue.Set(1f);
            Settings.PlayerAlphaValue.Deserialize(playerAlphaValue);
            Equal(0.35f, Settings.PlayerAlphaValue.Value, 1e-6f);

            Settings.MiniMapZoomIn.Set(Scancode.F6);
            var zoom = Settings.MiniMapZoomIn.Serialize();
            Settings.MiniMapZoomIn.Set(Scancode.Equals);
            Settings.MiniMapZoomIn.Deserialize(zoom);
            Equal(Scancode.F6, Settings.MiniMapZoomIn.Key);
        } finally {
            Settings.ResetToDefault();
        }
        Equal(true, Settings.AllyInfo.Value);
        Equal(true, Settings.AllyShots.Value);
        Equal(false, Settings.PlayerAlpha.Value);
        Equal(1f, Settings.PlayerAlphaValue.Value, 1e-6f);
        Equal("4", Settings.Cursor.Value);
        Equal(ParticleMode.On, Settings.EyeCandyParticles.Value);
        Equal(Scancode.Equals, Settings.MiniMapZoomIn.Key);
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
