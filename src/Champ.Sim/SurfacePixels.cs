namespace Champ.Sim
{
    /// <summary>
    /// Tileable ground texels, AARRGGBB, row 0 = top -- the same idea as
    /// <see cref="HeroPixels"/>, but repeated across a surface. One tile covers
    /// <see cref="WorldSize"/> world units.
    /// </summary>
    public static class SurfacePixels
    {
        public const int Size = 64;

        /// <summary>How many world units one tile spans before it repeats.</summary>
        public const float WorldSize = 8f;

        /// <summary>
        /// Deterministic hash. Hand-rolled rather than System.Random because the three
        /// renderers must agree texel for texel, and Random's sequence differs between
        /// .NET and the Mono runtime Unity compiles this against.
        /// </summary>
        static uint Hash(int x, int y, uint seed)
        {
            var h = (uint)(x * 374761393) + (uint)(y * 668265263) + seed * 2246822519u;
            h = (h ^ (h >> 13)) * 1274126177u;
            return h ^ (h >> 16);
        }

        /// <summary>
        /// One lattice corner in 0..1. Coordinates wrap at <paramref name="period"/>, which is
        /// what keeps the tile seamless when it repeats.
        /// </summary>
        static float Lattice(int x, int y, int period, uint seed) =>
            (Hash(((x % period) + period) % period, ((y % period) + period) % period, seed)
                & 0xFFFFu) / 65535f;

        /// <summary>
        /// Smooth value noise: a <paramref name="period"/>-cell lattice blended with a
        /// smoothstep falloff. Integer cell arithmetic keeps it exact, so every renderer
        /// gets identical texels.
        /// </summary>
        static float Noise(int x, int y, int period, uint seed)
        {
            var cell = Size / period;
            int x0 = x / cell, y0 = y / cell;
            var tx = (x % cell) / (float)cell;
            var ty = (y % cell) / (float)cell;
            tx = tx * tx * (3f - 2f * tx);
            ty = ty * ty * (3f - 2f * ty);

            var v00 = Lattice(x0, y0, period, seed);
            var v10 = Lattice(x0 + 1, y0, period, seed);
            var v01 = Lattice(x0, y0 + 1, period, seed);
            var v11 = Lattice(x0 + 1, y0 + 1, period, seed);
            var top = v00 + (v10 - v00) * tx;
            var bottom = v01 + (v11 - v01) * tx;
            return top + (bottom - top) * ty;
        }

        /// <summary>Stacked octaves, centred on zero. Broad blotches dominate, fine detail trims.</summary>
        static float Fbm(int x, int y, uint seed) =>
            Noise(x, y, 4, seed) * 0.55f +
            Noise(x, y, 8, seed + 17u) * 0.30f +
            Noise(x, y, 16, seed + 41u) * 0.15f - 0.5f;

        /// <summary>Barely-there per-texel grain, so broad gradients do not band.</summary>
        static int Grain(int x, int y, uint seed, int amount) =>
            (int)(Hash(x, y, seed) % (uint)(amount * 2 + 1)) - amount;

        /// <summary>Signed spread in [-amount, amount] from an existing hash.</summary>
        static int Spread(uint h, int amount) => (int)(h % (uint)(amount * 2 + 1)) - amount;

        static uint Pack(int r, int g, int b)
        {
            r = r < 0 ? 0 : r > 255 ? 255 : r;
            g = g < 0 ? 0 : g > 255 ? 255 : g;
            b = b < 0 ? 0 : b > 255 ? 255 : b;
            return (uint)(255 << 24 | r << 16 | g << 8 | b);
        }

        public static uint[] CreateArgb(SurfaceKind kind)
        {
            var p = new uint[Size * Size];
            for (var y = 0; y < Size; y++)
            for (var x = 0; x < Size; x++)
                p[y * Size + x] = Texel(kind, x, y);
            return p;
        }

        static uint Texel(SurfaceKind kind, int x, int y)
        {
            switch (kind)
            {
                case SurfaceKind.Grass:
                    return Grass(x, y);
                case SurfaceKind.Path:
                    return Path(x, y);
                case SurfaceKind.Wall:
                    // Small dark courses; the keep should read as coarser masonry than its floor.
                    return Masonry(x, y, 16, 8, 58, 54, 50, 13, 9, 401u);
                default:
                    // Flagstones: 2 world units square, one shade per stone.
                    return Masonry(x, y, 16, 16, 92, 86, 74, 19, 11, 233u);
            }
        }

        static uint Grass(int x, int y)
        {
            // Two uncorrelated fields: one shifts lightness, one shifts hue between
            // yellow-green and blue-green, so patches differ in tone as well as brightness.
            var shade = Fbm(x, y, 1u);
            var hue = Fbm(x, y, 71u);
            var grain = Grain(x, y, 907u, 2);

            var r = 74 + (int)(shade * 15f) + (int)(hue * 10f) + grain;
            var g = 108 + (int)(shade * 21f) + grain;
            var b = 62 + (int)(shade * 11f) - (int)(hue * 9f) + grain;
            return Pack(r, g, b);
        }

        /// <summary>
        /// Courses of block laid in running bond, each block its own shade, with a darker
        /// seam between them. Block sizes must divide <see cref="Size"/> so the tile stays
        /// seamless -- the half-block course offset then wraps cleanly too.
        /// </summary>
        static uint Masonry(
            int x, int y, int blockW, int blockH, int r, int g, int b, int seam, int spread, uint seed)
        {
            var course = y / blockH;
            var shifted = (x + (course % 2) * (blockW / 2)) % Size;
            var block = shifted / blockW;

            var tone = Hash(block, course, seed);
            var shade = Fbm(x, y, seed + 5u);
            var grain = Grain(x, y, seed + 97u, 2);

            // One lift for all three channels: stone varies in lightness, not hue. Spreading
            // the channels independently turns the courses into a purple/olive patchwork.
            var lift = Spread(tone, spread);
            var warm = Spread(tone >> 9, 3);

            var vr = r + lift + warm + (int)(shade * 6f) + grain;
            var vg = g + lift + (int)(shade * 6f) + grain;
            var vb = b + lift - warm + (int)(shade * 5f) + grain;

            // One-texel seam along the top and left edge of every block.
            if (shifted % blockW == 0 || y % blockH == 0)
            {
                vr -= seam;
                vg -= seam;
                vb -= seam;
            }

            return Pack(vr, vg, vb);
        }

        static uint Path(int x, int y)
        {
            var shade = Fbm(x, y, 2u);
            var grain = Grain(x, y, 613u, 2);

            var r = 134 + (int)(shade * 20f) + grain;
            var g = 110 + (int)(shade * 17f) + grain;
            var b = 78 + (int)(shade * 13f) + grain;

            // Pebbles fade in above a threshold instead of switching on, so they read as
            // soft-edged grit rather than speckle.
            var pebble = Noise(x, y, 32, 313u);
            if (pebble > 0.62f)
            {
                var t = (pebble - 0.62f) / 0.38f;
                r += (int)(t * 22f);
                g += (int)(t * 20f);
                b += (int)(t * 16f);
            }

            return Pack(r, g, b);
        }
    }
}
