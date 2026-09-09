using System;

namespace Champ.Sim
{
    /// <summary>
    /// Follow camera shared by every renderer: eases towards the hero, then clamps so the
    /// view never shows anything outside the world. Lives in the sim (not in a renderer) so
    /// all three engines pan identically -- the whole point of the comparison.
    /// </summary>
    public sealed class CameraFollow
    {
        /// <summary>
        /// Easing rate per second. Steady-state lag while running is
        /// <see cref="CastleWorld.Speed"/> / Smoothing = 1.25 world units, so the camera
        /// visibly trails the hero without shoving them off centre.
        /// </summary>
        public const float Smoothing = 6f;

        readonly Aabb _bounds;

        public CameraFollow(Aabb bounds, Vec2 start)
        {
            _bounds = bounds;
            Center = start;
        }

        public Vec2 Center { get; private set; }

        /// <summary>
        /// Advances the camera. The view size is passed per call because it changes with
        /// the window, and each renderer frames the world slightly differently.
        /// </summary>
        public void Update(Vec2 target, float viewWidth, float viewHeight, float dt)
        {
            // Exponential easing: frame-rate independent, unlike a fixed lerp factor.
            var t = dt > 0f ? 1f - MathF.Exp(-Smoothing * dt) : 1f;
            var x = Center.X + (target.X - Center.X) * t;
            var y = Center.Y + (target.Y - Center.Y) * t;

            // Clamp the eased result rather than the target: parked against an edge, the
            // camera then responds the instant the hero turns back instead of first
            // travelling home from somewhere outside the world.
            Center = new Vec2(
                ClampAxis(x, viewWidth, _bounds.Left, _bounds.Right),
                ClampAxis(y, viewHeight, _bounds.Bottom, _bounds.Top));
        }

        static float ClampAxis(float center, float view, float min, float max) =>
            max - min <= view
                ? (min + max) * 0.5f   // world narrower than the view: centre it
                : Math.Clamp(center, min + view * 0.5f, max - view * 0.5f);
    }
}
