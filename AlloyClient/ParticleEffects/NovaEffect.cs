using AlloyClient.Game;
using OpenTK.Mathematics;
using System;

namespace AlloyClient.ParticleEffects;

// Flash parity (NovaEffect): a one-shot radial burst whose ray count scales
// with the radius (4 + 2 per tile), unlike RingEffect's fixed 12.
internal class NovaEffect : ParticleEffect {

    private const int Size = 200;
    private const int Lifetime = 200;

    public float Radius;
    public uint Color;

    public NovaEffect(Vector2 position, float radius, uint color) {
        _position = position;
        Radius = radius;
        Color = color;
    }

    public override bool Update(double time, double dt) {
        var numPoints = 4 + (int)(Radius * 2);
        for (var i = 0; i < numPoints; i++) {
            var angle = i * 2 * MathF.PI / numPoints;
            var p = new Vector2(_position.X + Radius * MathF.Cos(angle), _position.Y + Radius * MathF.Sin(angle));
            Map.AddParticleEffect(new SparkerEffect(Size, Color, Lifetime, 0.5f, _position, p));
        }

        return false;
    }
}
