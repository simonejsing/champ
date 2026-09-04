namespace Champ.Sim
{
    public readonly struct Aabb
    {
        public float X { get; }
        public float Y { get; }
        public float W { get; }
        public float H { get; }

        public Aabb(float x, float y, float w, float h)
        {
            X = x;
            Y = y;
            W = w;
            H = h;
        }

        public float Left => X;
        public float Right => X + W;
        public float Bottom => Y;
        public float Top => Y + H;
        public float CenterX => X + W * 0.5f;
        public float CenterY => Y + H * 0.5f;

        public bool Intersects(Aabb other) =>
            Left < other.Right && Right > other.Left && Bottom < other.Top && Top > other.Bottom;
    }
}
