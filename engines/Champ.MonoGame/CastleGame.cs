using Champ.Sim;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Champ.MonoGame;

public sealed class CastleGame : Game
{
    readonly GraphicsDeviceManager _graphics;
    readonly CastleWorld _world = new();
    readonly CameraFollow _camera;
    SpriteBatch _spriteBatch = null!;
    Texture2D _hero = null!;
    Texture2D[] _surfaces = null!;

    public CastleGame()
    {
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1280,
            PreferredBackBufferHeight = 720
        };
        _camera = new CameraFollow(_world.Bounds, _world.Hero);
        IsMouseVisible = true;
        Window.Title = "Champ — MonoGame";
        Content.RootDirectory = "Content";
    }

    protected override void OnExiting(object sender, ExitingEventArgs args)
    {
        // DesktopGL can deadlock in OpenAL-soft / SDL_Quit during Dispose.
        Environment.Exit(0);
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _hero = CreateTexture(HeroPixels.CreateArgb(), HeroPixels.Width, HeroPixels.Height);

        var kinds = (SurfaceKind[])Enum.GetValues(typeof(SurfaceKind));
        _surfaces = new Texture2D[kinds.Length];
        foreach (var kind in kinds)
            _surfaces[(int)kind] = CreateTexture(
                SurfacePixels.CreateArgb(kind), SurfacePixels.Size, SurfacePixels.Size);
    }

    protected override void Update(GameTime gameTime)
    {
        var kb = Keyboard.GetState();
        if (kb.IsKeyDown(Keys.Escape))
            Exit();

        var x = 0f;
        var y = 0f;
        if (kb.IsKeyDown(Keys.A) || kb.IsKeyDown(Keys.Left)) x -= 1f;
        if (kb.IsKeyDown(Keys.D) || kb.IsKeyDown(Keys.Right)) x += 1f;
        if (kb.IsKeyDown(Keys.S) || kb.IsKeyDown(Keys.Down)) y -= 1f;
        if (kb.IsKeyDown(Keys.W) || kb.IsKeyDown(Keys.Up)) y += 1f;

        var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _world.Tick(dt, new Vec2(x, y));
        _camera.Update(_world.Hero, GraphicsDevice.Viewport.Width / ViewScale(), ViewHeight, dt);
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(34, 38, 44));
        // Negative Y scale (Y-up world) reverses triangle winding; default culling
        // would discard every sprite and leave only the clear color.
        _spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            SamplerState.PointWrap,
            DepthStencilState.None,
            RasterizerState.CullNone,
            null,
            CameraMatrix());

        // Surfaces are ordered back-to-front, so painting them in order is the depth test.
        foreach (var surface in _world.Surfaces)
            FillTiled(surface.Box, _surfaces[(int)surface.Kind]);
        var wallTexture = _surfaces[(int)SurfaceKind.Wall];
        foreach (var wall in _world.Walls)
            FillTiled(wall, wallTexture);

        var heroSize = CastleWorld.HeroHalf * 2f;
        _spriteBatch.Draw(
            _hero,
            new Vector2(_world.Hero.X, _world.Hero.Y),
            null,
            Color.White,
            0f,
            new Vector2(HeroPixels.Width * 0.5f, HeroPixels.Height * 0.5f),
            heroSize / HeroPixels.Width,
            SpriteEffects.None,
            0f);

        _spriteBatch.End();
        base.Draw(gameTime);
    }

    // Fixed visible height, width follows the window -- Stride and Unity frame the world
    // the same way, so all three show the same slice of world at any aspect ratio.
    const float ViewHeight = 24f;

    float ViewScale() => GraphicsDevice.Viewport.Height / ViewHeight;

    Matrix CameraMatrix()
    {
        var vp = GraphicsDevice.Viewport;
        var scale = ViewScale();
        var center = _camera.Center;
        return Matrix.CreateTranslation(-center.X, -center.Y, 0f)
               * Matrix.CreateScale(scale, -scale, 1f)
               * Matrix.CreateTranslation(vp.Width * 0.5f, vp.Height * 0.5f, 0f);
    }

    // A source rectangle larger than the texture is what makes PointWrap repeat the tile
    // across the surface; the scale then maps texels back to world units.
    void FillTiled(Aabb box, Texture2D texture)
    {
        const float texelsPerUnit = SurfacePixels.Size / SurfacePixels.WorldSize;
        var source = new Rectangle(
            0,
            0,
            (int)MathF.Round(box.W * texelsPerUnit),
            (int)MathF.Round(box.H * texelsPerUnit));
        _spriteBatch.Draw(
            texture,
            new Vector2(box.X, box.Y),
            source,
            Color.White,
            0f,
            Vector2.Zero,
            1f / texelsPerUnit,
            SpriteEffects.None,
            0f);
    }

    Texture2D CreateTexture(uint[] argb, int width, int height)
    {
        var colors = new Color[argb.Length];
        for (var i = 0; i < argb.Length; i++)
        {
            var p = argb[i];
            var a = (byte)(p >> 24);
            var r = (byte)(p >> 16);
            var g = (byte)(p >> 8);
            var b = (byte)p;
            colors[i] = new Color(r, g, b, a);
        }

        var tex = new Texture2D(GraphicsDevice, width, height);
        tex.SetData(colors);
        return tex;
    }

    protected override void UnloadContent()
    {
        _hero?.Dispose();
        if (_surfaces != null)
        {
            foreach (var texture in _surfaces)
                texture?.Dispose();
        }

        _spriteBatch?.Dispose();
        base.UnloadContent();
    }
}
