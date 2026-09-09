using Champ.Sim;
using Stride.CommunityToolkit.Bepu;
using Stride.CommunityToolkit.Engine;
using Stride.CommunityToolkit.Rendering.ProceduralModels;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Engine.Processors;
using Stride.Graphics;
using Stride.Games;
using Stride.Input;
using Stride.Rendering;
using Stride.Rendering.Materials;
using Stride.Rendering.Materials.ComputeColors;

using var game = new Game();

const float CameraHeight = 36f;
const float ViewHeight = 24f;   // Stride's OrthographicSize is the FULL height, not the half

Entity? hero = null;
Entity? cameraEntity = null;
var world = new CastleWorld();
var follow = new CameraFollow(world.Bounds, world.Hero);

game.Run(start: Start, update: Update);

void Start(Scene scene)
{
    game.SetupBase3D();
    game.SetCameraPosition(new Vector3(0f, CameraHeight, 0f));
    game.SetCameraRotation(new Vector3(0f, -90f, 0f));

    cameraEntity = scene.Entities.FirstOrDefault(e => e.Get<CameraComponent>() != null);
    if (cameraEntity is null)
        cameraEntity = game.SceneSystem.SceneInstance.RootScene.Entities
            .First(e => e.Get<CameraComponent>() != null);

    var camera = cameraEntity.Get<CameraComponent>();
    camera.Projection = CameraProjectionMode.Orthographic;
    camera.OrthographicSize = ViewHeight;

    // SetCameraRotation packs (Yaw, Pitch, Roll); a -90 Pitch here points the camera straight
    // down but leaves its up vector on world -Z. Sim Y (north-positive) maps to Z below, so
    // every Z placement is negated to keep north pointing up on screen -- otherwise the whole
    // keep (and WASD/arrow-key movement) reads vertically mirrored.
    for (var i = 0; i < world.Surfaces.Count; i++)
    {
        var surface = world.Surfaces[i];
        // Lift each patch by its index so overlapping ground never z-fights. The topmost
        // (last) patch lands at y = 0, exactly where the single floor slab used to sit, so
        // the walls and hero need no adjustment.
        var topY = (i - (world.Surfaces.Count - 1)) * 0.02f;
        var ground = game.Create3DPrimitive(PrimitiveModelType.Cube, new()
        {
            Size = new Vector3(surface.Box.W, 0.12f, surface.Box.H),
            Material = TexturedMaterial(surface.Kind, surface.Box.W, surface.Box.H),
            IncludeCollider = false
        });
        ground.Transform.Position = new Vector3(surface.Box.CenterX, topY - 0.06f, -surface.Box.CenterY);
        ground.Scene = scene;
    }

    foreach (var wall in world.Walls)
    {
        const float height = 2.2f;
        var block = game.Create3DPrimitive(PrimitiveModelType.Cube, new()
        {
            Size = new Vector3(wall.W, height, wall.H),
            Material = TexturedMaterial(SurfaceKind.Wall, wall.W, wall.H),
            IncludeCollider = false
        });
        block.Transform.Position = new Vector3(wall.CenterX, height * 0.5f, -wall.CenterY);
        block.Scene = scene;
    }

    hero = game.Create3DPrimitive(PrimitiveModelType.Cube, new()
    {
        Size = new Vector3(CastleWorld.HeroHalf * 2f, 1.1f, CastleWorld.HeroHalf * 2f),
        Material = game.CreateMaterial(new Color(52, 110, 186)),
        IncludeCollider = false
    });
    hero.Scene = scene;
    PlaceHero();
}

void Update(Scene scene, GameTime time)
{
    if (game.Input.IsKeyPressed(Keys.Escape))
        game.Exit();

    var x = 0f;
    var y = 0f;
    if (game.Input.IsKeyDown(Keys.A) || game.Input.IsKeyDown(Keys.Left)) x -= 1f;
    if (game.Input.IsKeyDown(Keys.D) || game.Input.IsKeyDown(Keys.Right)) x += 1f;
    if (game.Input.IsKeyDown(Keys.S) || game.Input.IsKeyDown(Keys.Down)) y -= 1f;
    if (game.Input.IsKeyDown(Keys.W) || game.Input.IsKeyDown(Keys.Up)) y += 1f;

    var dt = (float)time.Elapsed.TotalSeconds;
    world.Tick(dt, new Vec2(x, y));

    var backBuffer = game.GraphicsDevice.Presenter.BackBuffer;
    var viewWidth = backBuffer.Height > 0
        ? ViewHeight * backBuffer.Width / (float)backBuffer.Height
        : ViewHeight;
    follow.Update(world.Hero, viewWidth, ViewHeight, dt);
    if (cameraEntity is not null)
    {
        // Z negated for the same reason as every placement above: camera-up is world -Z.
        cameraEntity.Transform.Position =
            new Vector3(follow.Center.X, CameraHeight, -follow.Center.Y);
    }

    PlaceHero();
}

void PlaceHero()
{
    if (hero is null)
        return;
    hero.Transform.Position = new Vector3(world.Hero.X, 0.55f, -world.Hero.Y);
}

// The toolkit's CreateMaterial only takes a flat colour, so build the material by hand to
// get a tiling diffuse map out of the shared ground texels.
Material TexturedMaterial(SurfaceKind kind, float width, float height)
{
    var argb = SurfacePixels.CreateArgb(kind);
    var data = new byte[argb.Length * 4];
    for (var i = 0; i < argb.Length; i++)
    {
        var p = argb[i];
        data[i * 4] = (byte)(p >> 16);
        data[i * 4 + 1] = (byte)(p >> 8);
        data[i * 4 + 2] = (byte)p;
        data[i * 4 + 3] = (byte)(p >> 24);
    }

    var texture = Texture.New2D(
        game.GraphicsDevice,
        SurfacePixels.Size,
        SurfacePixels.Size,
        PixelFormat.R8G8B8A8_UNorm_SRgb,
        data);

    var map = new ComputeTextureColor(texture)
    {
        AddressModeU = TextureAddressMode.Wrap,
        AddressModeV = TextureAddressMode.Wrap,
        Filtering = TextureFilter.Point,
        Scale = new Vector2(
            width / SurfacePixels.WorldSize,
            height / SurfacePixels.WorldSize)
    };

    return Material.New(game.GraphicsDevice, new MaterialDescriptor
    {
        Attributes =
        {
            Diffuse = new MaterialDiffuseMapFeature(map),
            DiffuseModel = new MaterialDiffuseLambertModelFeature(),
            // 1.0f matches the toolkit's CreateMaterial default; 0f leaves the surface fully
            // dielectric, which adds a full Lambert diffuse term and washes the ground out.
            Specular = new MaterialMetalnessMapFeature(new ComputeFloat(1.0f)),
            SpecularModel = new MaterialSpecularMicrofacetModelFeature(),
            MicroSurface = new MaterialGlossinessMapFeature(new ComputeFloat(0.65f))
        }
    });
}
