using AlloyClient.Game;
using OpenTK.Mathematics;
using System;

namespace AlloyClient.ParticleEffects;

internal class SparkerEffect : ParticleEffect {
    // Flash parity (SparkerParticle): a mover that emits one trail dot per
    // frame at 60fps. Emission is normalized to wall-clock time so uncapped
    // framerates don't multiply the particle load: one child per ~16.7ms of
    // life, with a small per-frame cap so a hitch emits a bounded burst
    // instead of the whole backlog at once.
    private const double ReferenceStepMs = 1000.0 / 60.0;
    private const int MaxEmitsPerUpdate = 4;

    private double _timeLeft;
    private uint _color;
    private int _trailLifetime;

    private float _dx;
    private float _dy;
    private double _emitAcc;

    public SparkerEffect(int size, uint color, int lifetime, float z, Vector2 start, Vector2 end, int trailLifetime = 600) {
        _position = start;
        _color = color;
        _timeLeft = lifetime;
        _trailLifetime = trailLifetime;
        _dx = (end.X - start.X) / (lifetime / 1000f);
        _dy = (end.Y - start.Y) / (lifetime / 1000f);
    }

    public override bool Update(double time, double dt) {
        _timeLeft -= dt;
        if (_timeLeft <= 0) return false;

        var delta = (float)(dt / 1000.0);
        _position += delta * new Vector2(_dx, _dy);
        _emitAcc += dt / ReferenceStepMs;
        var emits = Math.Min((int)_emitAcc, MaxEmitsPerUpdate);
        _emitAcc -= emits;
        for (var i = 0; i < emits; i++) {
            Map.AddParticleEffect(new SparkEffect(100, _color, _trailLifetime, 0.5f, _position,
                _position + new Vector2(Random.Shared.NextSingle() * 2 - 1, Random.Shared.NextSingle() * 2 - 1)));
        }

        return true;
    }
}
