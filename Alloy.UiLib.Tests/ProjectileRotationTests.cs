using System.Xml.Linq;
using Alloy.Engine;
using AlloyClient;
using AlloyClient.Assets;
using AlloyClient.Assets.Libraries;
using AlloyClient.Assets.XmlStructs;
using AlloyClient.Game;
using AlloyClient.Game.Objects;
using OpenTK.Mathematics;

namespace Alloy.UiLib.Tests;

// The Flash client draws a projectile at (travel angle - camera angle +
// angle correction) in screen space (Projectile.as draw). The OpenTK port
// must counter-rotate by the camera angle: at camera 0 the sprite matches
// the travel direction, and rotating the camera must rotate the sprite back
// so it stays locked to the world instead of rotating with the screen.
internal static class ProjectileRotationTests {
    private const ushort TestType = 0xF000;
    private const float ShootAngle = 0.7f;

    public static void Run() {
        var hadTexture = ObjectLibrary.TypeToTextureData.TryGetValue(TestType, out var prevTexture);
        ObjectLibrary.TypeToTextureData[TestType] = new TextureData(new XElement("Object"));
        var prevCamera = Settings.CameraAngle.Value;
        try {
            foreach (var camera in new[] { 0f, 0.5f, -1f, 1f, 2f }) {
                StaysWorldLocked(camera, 0f);
            }
            StaysWorldLocked(0.5f, 2f);
        } finally {
            if (hadTexture) {
                ObjectLibrary.TypeToTextureData[TestType] = prevTexture!;
            } else {
                ObjectLibrary.TypeToTextureData.Remove(TestType);
            }
            Settings.CameraAngle.Set(prevCamera);
        }
    }

    private static void StaysWorldLocked(float cameraAngle, float angleCorrectionSteps) {
        Settings.CameraAngle.Set(cameraAngle);

        var objDesc = new ObjectProperties(XElement.Parse(
            $"<Object type=\"{TestType}\" id=\"rotation_test_bullet\">" +
            "<Class>Projectile</Class>" +
            $"<AngleCorrection>{angleCorrectionSteps}</AngleCorrection>" +
            "</Object>"));
        var projDesc = new ProjectileProperties(XElement.Parse(
            "<Projectile id=\"0\">" +
            "<ObjectId>rotation_test_bullet</ObjectId>" +
            "<LifetimeMS>1000</LifetimeMS>" +
            "<Speed>100</Speed>" +
            "</Projectile>"));

        var projectile = new Projectile();
        projectile.Reset(1, 10, ShootAngle, new Entity(), objDesc, projDesc, null,
            new Vector2(10.5f, 10.5f));

        if (!projectile.Update(new GameTime(16, 16))) {
            throw new Exception("expected the test projectile to keep flying");
        }

        var matrix = Matrix4.Identity;
        var depth = new DepthMatrix(in matrix);
        var rotation = projectile.Draw(in depth).Rotation;
        var actual = -MathF.Atan2(rotation.X, rotation.Y);
        var expected = ShootAngle - cameraAngle + angleCorrectionSteps * MathHelper.PiOver4;

        if (MathF.Abs(actual - expected) > 1e-3f) {
            throw new Exception($"camera {cameraAngle}: expected rotation {expected}, got {actual}");
        }
    }
}
