using System;

namespace Champ.Sim
{
    public readonly struct Vec2
    {
        public float X { get; }
        public float Y { get; }

        public Vec2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public float LengthSquared => X * X + Y * Y;

        public Vec2 Normalized()
        {
            var len = MathF.Sqrt(LengthSquared);
            return len < 0.0001f ? default : new Vec2(X / len, Y / len);
        }

        public static Vec2 operator +(Vec2 a, Vec2 b) => new(a.X + b.X, a.Y + b.Y);
        public static Vec2 operator *(Vec2 a, float s) => new(a.X * s, a.Y * s);
    }
}
