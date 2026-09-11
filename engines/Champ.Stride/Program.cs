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
using Stride.Rendering.Compositing;
using Stride.Rendering.Images;
using Stride.Rendering.Lights;
using Stride.Rendering.Materials;
using Stride.Rendering.Materials.ComputeColors;

using var game = new Game();

const float CameraHeight = 36f;
const float ViewHeight = 24f;   // Stride's OrthographicSize is the FULL height, not the half
const float HeroHeight = 1.1f;
const int HeroShadowStrips = 8;   // across the shadow's width; each is clipped by walls on its own

Entity? hero = null;
Entity[] heroShadow = Array.Empty<Entity>();
var heroShadowAlong = Vector3.Zero;   // unit direction his shadow falls, in world space
var heroShadowSpan = 0f;              // its full length on open ground
Entity? cameraEntity = null;
var world = new CastleWorld();
var follow = new CameraFollow(world.Bounds, world.Hero);

game.Run(start: Start, update: Update);

void Start(Scene scene)
{
    game.SetupBase3D();

    // The tone map's auto-exposure renormalises every frame to its average brightness, so the
    // whole scene brightened or dimmed as the view moved between lit and shadowed ground. A
    // fixed exposure keeps a patch of ground the same brightness wherever the camera is.
    if (FindForwardRenderer(game.SceneSystem.GraphicsCompositor?.Game) is { PostEffects: PostProcessingEffects effects })
    {
        foreach (var transform in effects.ColorTransforms.Transforms)
        {
            if (transform is ToneMap toneMap)
                toneMap.AutoExposure = false;
        }
    }
    game.SetCameraPosition(new Vector3(0f, CameraHeight, 0f));
    game.SetCameraRotation(new Vector3(0f, -90f, 0f));

    cameraEntity = scene.Entities.FirstOrDefault(e => e.Get<CameraComponent>() != null);
    if (cameraEntity is null)
        cameraEntity = game.SceneSystem.SceneInstance.RootScene.Entities
            .First(e => e.Get<CameraComponent>() != null);

    // Take the key light before the fill joins it -- right now it is the only directional light.
    var keyLight = scene.Entities.Concat(game.SceneSystem.SceneInstance.RootScene.Entities)
        .FirstOrDefault(e => e.Get<LightComponent>()?.Type is LightDirectional);

    // SetupBase3D installs one shadow-casting directional light and nothing else, so anything
    // inside a shadow gets zero light and renders pure black -- the hero vanished entirely
    // whenever he stepped into a wall's shadow. A LightAmbient is ignored by this compositor,
    // so fill with a dim shadow-less light pointing straight down: it lifts shadowed ground
    // while leaving the key light's shadows visible.
    // With every material diffuse, lit ground is key + fill and shadowed ground is fill alone,
    // wherever it sits on screen. The toolkit's key (intensity 20) is far too strong for diffuse
    // surfaces, so it is scaled down; the two values together set how bright lit ground is and
    // how dark shadows are relative to it.
    if (keyLight?.Get<LightComponent>() is { } key)
        key.Intensity *= 0.116f;

    var fill = game.AddDirectionalLight(entityName: "Fill", enableShadows: false, intensity: 0.31f);
    fill.Transform.Rotation = Quaternion.RotationYawPitchRoll(
        0f, MathUtil.DegreesToRadians(-90f), 0f);

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
        Size = new Vector3(CastleWorld.HeroHalf * 2f, HeroHeight, CastleWorld.HeroHalf * 2f),
        Material = FlatMaterial(new Color(52, 110, 186)),
        IncludeCollider = false
    });
    hero.Scene = scene;
    CreateHeroShadow(scene, keyLight);
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

static ForwardRenderer? FindForwardRenderer(ISceneRenderer? renderer) => renderer switch
{
    ForwardRenderer forward => forward,
    SceneCameraRenderer camera => FindForwardRenderer(camera.Child),
    SceneRendererCollection collection =>
        collection.Children.Select(FindForwardRenderer).FirstOrDefault(found => found is not null),
    _ => null
};

void PlaceHero()
{
    if (hero is null)
        return;
    var basePosition = new Vector3(world.Hero.X, 0f, -world.Hero.Y);
    hero.Transform.Position = basePosition + new Vector3(0f, HeroHeight * 0.5f, 0f);

    // Just clear of the stone floor at y = 0, and so also of the lower grass and path.
    // A real shadow stops at the first wall it meets and climbs its face; lying flat on the
    // ground, the quad would slide under a wall and reappear on the far side. Each strip runs
    // from under the hero along the shadow until the first wall in its own path, so a wall
    // catching one edge shortens only that edge. The outermost strips' centre lines sit inside
    // the hero's footprint, so a wall he is merely standing beside never counts as a hit.
    var direction = new Vec2(heroShadowAlong.X, -heroShadowAlong.Z);   // sim Y is world -Z
    var side = new Vec2(-direction.Y, direction.X);
    for (var i = 0; i < heroShadow.Length; i++)
    {
        var offset = ((i + 0.5f) / heroShadow.Length - 0.5f) * CastleWorld.HeroHalf * 2f;
        var origin = world.Hero + side * offset;
        // Never scale to zero: that makes a degenerate matrix.
        var length = MathF.Max(world.CastRay(origin, direction, heroShadowSpan), 0.01f);
        var centre = origin + direction * (length * 0.5f);
        heroShadow[i].Transform.Scale = new Vector3(1f, 1f, length);
        heroShadow[i].Transform.Position = new Vector3(centre.X, 0.02f, -centre.Y);
    }
}

// One shadow map cannot stack occlusions -- a point is either shadowed or not -- so the hero's
// shadow simply merged into any wall shadow he stood in. Instead of casting one, he gets an
// equivalent projected quad laid exactly where the key light throws his shadow. As a
// multiply-darken it reads as an ordinary shadow on lit ground and deepens ground that is
// already in shadow.
void CreateHeroShadow(Scene scene, Entity? keyLight)
{
    if (hero is null || keyLight is null)
        return;   // no key light found: leave the hero casting his real shadow

    // Lights shine along their local Z axis. Flip the ray if needed so it travels downwards;
    // that keeps this independent of which way the axis convention points.
    var ray = Vector3.Transform(-Vector3.UnitZ, keyLight.Transform.Rotation);
    if (ray.Y > 0f)
        ray = -ray;
    if (ray.Y > -0.05f)
        return;   // grazing light: not worth faking a shadow that long

    // Where the top of the hero lands on the ground, relative to his base.
    var reach = new Vector3(ray.X, 0f, ray.Z) * (HeroHeight / -ray.Y);
    var length = reach.Length();
    if (length < 0.01f)
        return;   // light from straight overhead: the shadow hides under the hero anyway

    var along = reach / length;
    var width = CastleWorld.HeroHalf * 2f;

    // Runs from under the hero's centre, where his body hides it, to the far edge of his
    // projected top face. The near end is no wider than his footprint, so nothing rings him.
    var span = length + width * (MathF.Abs(along.X) + MathF.Abs(along.Z)) * 0.5f;
    heroShadowAlong = along;
    heroShadowSpan = span;

    // Strips one unit long: PlaceHero stretches each to wherever the first wall stops it. The
    // shared rotation turns a strip's local X onto the same side vector PlaceHero offsets along.
    var material = ShadowMaterial();
    var rotation = Quaternion.RotationY(MathF.Atan2(along.X, along.Z));
    heroShadow = new Entity[HeroShadowStrips];
    for (var i = 0; i < HeroShadowStrips; i++)
    {
        var strip = game.Create3DPrimitive(PrimitiveModelType.Cube, new()
        {
            Size = new Vector3(width / HeroShadowStrips, 0.02f, 1f),
            Material = material,
            IncludeCollider = false
        });
        strip.Transform.Rotation = rotation;
        if (strip.Get<ModelComponent>() is { } stripModel)
            stripModel.IsShadowCaster = false;
        strip.Scene = scene;
        heroShadow[i] = strip;
    }

    // The quad replaces his real shadow; keeping both would double-darken lit ground.
    if (hero.Get<ModelComponent>() is { } heroModel)
        heroModel.IsShadowCaster = false;
}

// Black albedo is black under any light, so the quad only ever darkens what is behind it.
// The hero's real shadow -- a small caster, softened by the shadow filter -- only takes lit
// floor to about 85% (measured 147 -> 125). This runs a little stronger than that so that,
// laid over a wall's shadow, the same multiply makes the overlap clearly darker.
Material ShadowMaterial() => Material.New(game.GraphicsDevice, new MaterialDescriptor
{
    Attributes =
    {
        Diffuse = new MaterialDiffuseMapFeature(new ComputeColor(new Color4(0f, 0f, 0f, 1f))),
        DiffuseModel = new MaterialDiffuseLambertModelFeature(),
        Transparency = new MaterialTransparencyBlendFeature { Alpha = new ComputeFloat(0.22f) }
    }
});

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
            DiffuseModel = new MaterialDiffuseLambertModelFeature()
        }
    });
}

// Diffuse only, like the ground. The toolkit's CreateMaterial is fully metallic, and a metal's
// shading is all reflection, which depends on where the camera sits -- surfaces brightened
// toward the middle of the screen and darkened toward its edges, so the hero changed colour as
// the camera stopped following him at the map edge.
Material FlatMaterial(Color color) => Material.New(game.GraphicsDevice, new MaterialDescriptor
{
    Attributes =
    {
        Diffuse = new MaterialDiffuseMapFeature(new ComputeColor(color)),
        DiffuseModel = new MaterialDiffuseLambertModelFeature()
    }
});
