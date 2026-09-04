namespace Champ.Sim
{
    /// <summary>
    /// 16×16 champion pixels, row 0 = top of the sprite, AARRGGBB.
    /// </summary>
    public static class HeroPixels
    {
        public const int Width = 16;
        public const int Height = 16;

        public static uint[] CreateArgb()
        {
            var p = new uint[Width * Height];
            uint C(byte r, byte g, byte b, byte a = 255) =>
                (uint)(a << 24 | r << 16 | g << 8 | b);

            var skin = C(232, 196, 156);
            var hair = C(92, 52, 28);
            var tunic = C(52, 110, 186);
            var belt = C(62, 42, 28);
            var boot = C(48, 32, 24);
            var eye = C(24, 24, 28);
            var cape = C(164, 48, 48);

            void Set(int x, int y, uint c)
            {
                if ((uint)x < Width && (uint)y < Height)
                    p[y * Width + x] = c;
            }

            for (var y = 2; y <= 7; y++)
            for (var x = 11; x <= 14; x++)
                Set(x, y, cape);

            for (var y = 1; y <= 5; y++)
            for (var x = 5; x <= 10; x++)
                Set(x, y, hair);

            for (var y = 3; y <= 7; y++)
            for (var x = 6; x <= 9; x++)
                Set(x, y, skin);

            Set(7, 5, eye);
            Set(9, 5, eye);

            for (var y = 8; y <= 12; y++)
            for (var x = 5; x <= 10; x++)
                Set(x, y, tunic);

            for (var x = 5; x <= 10; x++)
                Set(x, 11, belt);

            for (var y = 13; y <= 15; y++)
            {
                Set(6, y, boot);
                Set(7, y, boot);
                Set(8, y, boot);
                Set(9, y, boot);
            }

            return p;
        }
    }
}
