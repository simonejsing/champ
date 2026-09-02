using Champ.Sim;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Champ.MonoGame;

public sealed class CastleGame : Game
{
    readonly GraphicsDeviceManager _graphics;
    readonly CastleWorld _world = new();
    SpriteBatch _spriteBatch = null!;
    Texture2D _pixel = null!;
    Texture2D _hero = null!;

    public CastleGame()
    {
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 1280,
            PreferredBackBufferHeight = 720
        };
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
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _hero = CreateHeroTexture();
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

        _world.Tick((float)gameTime.ElapsedGameTime.TotalSeconds, new Vec2(x, y));
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
            SamplerState.PointClamp,
            DepthStencilState.None,
            RasterizerState.CullNone,
            null,
            CameraMatrix());

        Fill(_world.Floor, new Color(92, 86, 74));
        foreach (var wall in _world.Walls)
            Fill(wall, new Color(58, 54, 50));

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

    Matrix CameraMatrix()
    {
        var vp = GraphicsDevice.Viewport;
        const float worldW = 36f;
        const float worldH = 24f;
        var scale = MathF.Min(vp.Width / worldW, vp.Height / worldH);
        return Matrix.CreateScale(scale, -scale, 1f)
               * Matrix.CreateTranslation(vp.Width * 0.5f, vp.Height * 0.5f, 0f);
    }

    void Fill(Aabb box, Color color)
    {
        _spriteBatch.Draw(
            _pixel,
            new Vector2(box.X, box.Y),
            null,
            color,
            0f,
            Vector2.Zero,
            new Vector2(box.W, box.H),
            SpriteEffects.None,
            0f);
    }

    Texture2D CreateHeroTexture()
    {
        var argb = HeroPixels.CreateArgb();
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

        var tex = new Texture2D(GraphicsDevice, HeroPixels.Width, HeroPixels.Height);
        tex.SetData(colors);
        return tex;
    }

    protected override void UnloadContent()
    {
        _pixel?.Dispose();
        _hero?.Dispose();
        _spriteBatch?.Dispose();
        base.UnloadContent();
    }
}
