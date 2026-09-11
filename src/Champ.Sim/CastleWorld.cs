using System;
using System.Collections.Generic;

namespace Champ.Sim
{
    /// <summary>
    /// Top-down world: Y-up, X-right, origin at the courtyard centre. The keep sits in
    /// the middle of a much larger outdoor map -- see <see cref="Bounds"/>.
    /// </summary>
    public sealed class CastleWorld
    {
        public const float HeroHalf = 0.4f;
        public const float Speed = 7.5f;

        /// <summary>How far a torch's light reaches.</summary>
        public const float TorchRange = 7f;

        public Vec2 Hero { get; private set; }

        /// <summary>Full extent of the map. The hero is clamped inside it.</summary>
        public Aabb Bounds { get; }

        /// <summary>
        /// Ground patches ordered back-to-front: later entries paint over earlier ones.
        /// 3D renderers should lift entry i by i * step so overlapping patches do not
        /// z-fight -- deriving elevation from the index keeps it in step with paint order.
        /// </summary>
        public IReadOnlyList<Surface> Surfaces { get; }

        public IReadOnlyList<Aabb> Walls { get; }

        /// <summary>
        /// Torch posts: around all four outer walls, flanking the south gate, down both sides of
        /// the path, and through the rooms inside the keep. Decoration only -- they don't block
        /// the hero.
        /// </summary>
        public IReadOnlyList<Vec2> Torches { get; }

        public CastleWorld()
        {
            Hero = new Vec2(0f, -7.2f);
            Bounds = new Aabb(-60f, -40f, 120f, 80f);
            Surfaces = BuildSurfaces(Bounds);
            Walls = BuildKeep();
            Torches = BuildTorches();
        }

        public void Tick(float dt, Vec2 input)
        {
            var delta = input.Normalized() * (Speed * dt);
            // Clamp each candidate to the map before testing it, so running into the world
            // edge slides along it the same way running into a wall does.
            var x = Math.Clamp(Hero.X + delta.X, Bounds.Left + HeroHalf, Bounds.Right - HeroHalf);
            if (!Collides(new Vec2(x, Hero.Y)))
                Hero = new Vec2(x, Hero.Y);
            var y = Math.Clamp(Hero.Y + delta.Y, Bounds.Bottom + HeroHalf, Bounds.Top - HeroHalf);
            if (!Collides(new Vec2(Hero.X, y)))
                Hero = new Vec2(Hero.X, y);
        }

        /// <summary>
        /// Distance from <paramref name="origin"/> along the unit <paramref name="direction"/> to
        /// the first wall, or <paramref name="maxDistance"/> if nothing is hit sooner. A ray that
        /// only grazes a wall face does not count as a hit.
        /// </summary>
        public float CastRay(Vec2 origin, Vec2 direction, float maxDistance)
        {
            var nearest = maxDistance;
            for (var i = 0; i < Walls.Count; i++)
            {
                var wall = Walls[i];
                var enter = 0f;
                var exit = nearest;
                if (Slab(origin.X, direction.X, wall.Left, wall.Right, ref enter, ref exit) &&
                    Slab(origin.Y, direction.Y, wall.Bottom, wall.Top, ref enter, ref exit))
                    nearest = enter;
            }

            return nearest;
        }

        // Narrows [enter, exit] to where the ray is between min and max on one axis.
        static bool Slab(float origin, float direction, float min, float max, ref float enter, ref float exit)
        {
            if (MathF.Abs(direction) < 1e-6f)
                return origin > min && origin < max;

            var t1 = (min - origin) / direction;
            var t2 = (max - origin) / direction;
            if (t1 > t2)
            {
                var swap = t1;
                t1 = t2;
                t2 = swap;
            }

            if (t1 > enter)
                enter = t1;
            if (t2 < exit)
                exit = t2;
            return enter < exit;
        }

        /// <summary>
        /// Torchlight arriving at <paramref name="point"/>; 0 where no torch reaches. Each torch
        /// within <see cref="TorchRange"/> adds a smooth falloff, unless a wall stands between
        /// them. A point inside a wall counts as lit when the torch can see that wall's nearest
        /// face, so renderers can light wall tops as well as the ground.
        /// </summary>
        public float TorchLight(Vec2 point)
        {
            const float rangeSq = TorchRange * TorchRange;
            var total = 0f;
            for (var i = 0; i < Torches.Count; i++)
            {
                var torch = Torches[i];
                var dx = point.X - torch.X;
                var dy = point.Y - torch.Y;
                var distSq = dx * dx + dy * dy;
                if (distSq >= rangeSq)
                    continue;

                var target = point;
                if (WallAt(point) is { } wall)
                    target = new Vec2(
                        Math.Clamp(torch.X, wall.Left, wall.Right),
                        Math.Clamp(torch.Y, wall.Bottom, wall.Top));

                var tx = target.X - torch.X;
                var ty = target.Y - torch.Y;
                var reach = MathF.Sqrt(tx * tx + ty * ty);
                if (reach > 1e-4f && CastRay(torch, new Vec2(tx / reach, ty / reach), reach) < reach - 1e-3f)
                    continue;   // a wall is in the way

                var falloff = 1f - distSq / rangeSq;
                total += falloff * falloff;
            }

            return total;
        }

        Aabb? WallAt(Vec2 point)
        {
            for (var i = 0; i < Walls.Count; i++)
            {
                var wall = Walls[i];
                if (point.X > wall.Left && point.X < wall.Right && point.Y > wall.Bottom && point.Y < wall.Top)
                    return wall;
            }

            return null;
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

        static List<Surface> BuildSurfaces(Aabb bounds)
        {
            // Back-to-front. The path is 4 wide to match the south gate opening and runs
            // from the south map edge up to the keep's outer wall.
            return new List<Surface>
            {
                new(bounds, SurfaceKind.Grass),
                new(new Aabb(-2f, -40f, 4f, 30f), SurfaceKind.Path),
                new(new Aabb(-16f, -10f, 32f, 20f), SurfaceKind.Stone),
            };
        }

        static List<Vec2> BuildTorches()
        {
            // Outside, 2.5 units clear of the outer wall faces (the walls span x +-16, y +-10);
            // inside, one in each room. Walls block their light (see TorchLight), so a torch on
            // one side of a wall never lights the other.
            return new List<Vec2>
            {
                // South wall; the +-3 pair flanks the 4-wide gate.
                new(-14f, -12.5f), new(-8f, -12.5f), new(-3f, -12.5f), new(3f, -12.5f), new(8f, -12.5f), new(14f, -12.5f),
                // North wall
                new(-14f, 12.5f), new(-7f, 12.5f), new(0f, 12.5f), new(7f, 12.5f), new(14f, 12.5f),
                // West and east walls
                new(-18.5f, -6f), new(-18.5f, 0f), new(-18.5f, 6f),
                new(18.5f, -6f), new(18.5f, 0f), new(18.5f, 6f),
                // Corners
                new(-18.5f, -12.5f), new(18.5f, -12.5f), new(-18.5f, 12.5f), new(18.5f, 12.5f),
                // Both sides of the 4-wide path
                new(-3f, -18f), new(3f, -18f), new(-3f, -25f), new(3f, -25f), new(-3f, -32f), new(3f, -32f),
                // Inside the keep: courtyard, both side chambers, the throne area between them, the
                // two side corridors and the north room.
                new(-7f, -6f), new(7f, -6f), new(-7f, 0f), new(7f, 0f),
                new(-7f, 4.3f), new(7f, 4.3f),
                new(0f, 5f),
                new(-13f, 3f), new(13f, 3f),
                new(-9f, 8f), new(9f, 8f),
            };
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
                // Inner east chamber (mirror of west: outer face at x = 11)
                new(11f - t, -3f, t, 9f),
                new(4f, 6f, 7f, t),
                new(4f - t, 2f, t, 4f + t),
                // Throne dais wall with a centre opening
                new(-8f, 1.5f, 6.2f, t),
                new(1.8f, 1.5f, 6.2f, t),
            };
            return walls;
        }
    }
}
