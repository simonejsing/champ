namespace Champ.Sim
{
    /// <summary>
    /// Material kinds the world is built from. <see cref="CastleWorld.Surfaces"/> only ever
    /// holds the ground kinds; <see cref="Wall"/> exists so walls draw from the same
    /// texel source as everything else.
    /// </summary>
    public enum SurfaceKind
    {
        Grass,
        Path,
        Stone,
        Wall
    }

    /// <summary>
    /// A flat patch of ground. Renderers map <see cref="Kind"/> to a colour; how high
    /// the patch sits comes from its index in <see cref="CastleWorld.Surfaces"/>, so
    /// paint order and elevation can never disagree.
    /// </summary>
    public readonly struct Surface
    {
        public Aabb Box { get; }
        public SurfaceKind Kind { get; }

        public Surface(Aabb box, SurfaceKind kind)
        {
            Box = box;
            Kind = kind;
        }
    }
}
