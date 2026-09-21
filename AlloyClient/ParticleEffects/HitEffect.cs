using System;
using AlloyClient.Game;
using AlloyClient.Game.Objects;
using AlloyClient.Rendering.VertexData;
using OpenTK.Mathematics;

namespace AlloyClient.ParticleEffects;

public class HitEffect : ParticleEffect
{
    private const int Buffer = 10;
    private readonly HitParticle[] _data = new HitParticle[Buffer];
    private readonly ParticleData[] _particles = new ParticleData[Buffer];
    
    private readonly Entity _parent;

    private readonly Vector4 _color;
    private int _count;
    private bool _spawned;

    public HitEffect(Entity entity, uint color) {
        _parent = entity;
        _color = GetColor(color);
    }

    public override bool Update(double time, double dt)
    {
        // Flash HitEffect.update is one-shot: it emits all 10 particles at
        // once, each with its own 200 + random * 100ms lifetime, then dies.
        if (!_spawned)
        {
            _spawned = true;

            for (; _count < Buffer; _count++)
            {
                var dx = (float)(Random.Shared.NextDouble() - 0.5) * 0.4f;
                var dy = (float)(Random.Shared.NextDouble() - 0.5) * 0.4f;

                _data[_count] = new HitParticle(dx, dy, 200 + Random.Shared.NextDouble() * 100);
                _particles[_count] = new ParticleData(new Vector3(_parent.Position.X, _parent.Position.Y, 0.5f), _color);
            }
        }

        for (var i = _count - 1; i >= 0; i--)
        {
            ref var data = ref _data[i];

            data.TimeLeft -= dt;
            if (data.TimeLeft <= 0)
            {
                _count--;
                _data[i] = _data[_count];
                _particles[i] = _particles[_count];
                continue;
            }

            ref var particle = ref _particles[i];

            particle.Position.X += data.X * (float)(dt * 0.008);
            particle.Position.Y += data.Y * (float)(dt * 0.008);

            particle.Color = _color;
        }

        if (_count == 0) {
            return false;
        }

        Map.AddParticles(_particles, _count);

        return true;
    }
}