using System.Collections.Generic;

namespace Champ.Sim;

/// <summary>
/// Top-down keep: Y-up, X-right, origin at the courtyard centre.
/// </summary>
public sealed class CastleWorld
{
    public const float HeroHalf = 0.4f;
    public const float Speed = 7.5f;

    public Vec2 Hero { get; private set; }
    public Aabb Floor { get; }
    public IReadOnlyList<Aabb> Walls { get; }

    public CastleWorld()
    {
        Hero = new Vec2(0f, -7.2f);
        Floor = new Aabb(-16f, -10f, 32f, 20f);
        Walls = BuildKeep();
    }

    public void Tick(float dt, Vec2 input)
    {
        var delta = input.Normalized() * (Speed * dt);
        var x = Hero.X + delta.X;
        if (!Collides(new Vec2(x, Hero.Y)))
            Hero = new Vec2(x, Hero.Y);
        var y = Hero.Y + delta.Y;
        if (!Collides(new Vec2(Hero.X, y)))
            Hero = new Vec2(Hero.X, y);
    }

    bool Collides(Vec2 position)
    {
        var box = new Aabb(
            position.X - HeroHalf,
            position.Y - HeroHalf,
            HeroHalf * 2f,
            HeroHalf * 2f);
        for (var i = 0; i < Walls.Count; i++)
        {
            if (box.Intersects(Walls[i]))
                return true;
        }

        return false;
    }

    static List<Aabb> BuildKeep()
    {
        const float t = 1.2f;
        var walls = new List<Aabb>
        {
            // Outer shell, south door opening from x = -2 to 2
            new(-16f, -10f, 14f, t),
            new(2f, -10f, 14f, t),
            new(-16f, 10f - t, 32f, t),
            new(-16f, -10f, t, 20f),
            new(16f - t, -10f, t, 20f),
            // Inner west chamber
            new(-11f, -3f, t, 9f),
            new(-11f, 6f, 7f, t),
            new(-4f, 2f, t, 4f + t),
            // Inner east chamber
            new(10f - t, -3f, t, 9f),
            new(4f, 6f, 7f, t),
            new(4f - t, 2f, t, 4f + t),
            // Throne dais wall with a centre opening
            new(-8f, 1.5f, 6.2f, t),
            new(1.8f, 1.5f, 6.2f, t),
        };
        return walls;
    }
}
