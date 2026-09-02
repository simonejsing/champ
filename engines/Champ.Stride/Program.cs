using Champ.Sim;
using Stride.CommunityToolkit.Bepu;
using Stride.CommunityToolkit.Engine;
using Stride.CommunityToolkit.Rendering.ProceduralModels;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Engine.Processors;
using Stride.Games;
using Stride.Input;

using var game = new Game();

Entity? hero = null;
var world = new CastleWorld();

game.Run(start: Start, update: Update);

void Start(Scene scene)
{
    game.SetupBase3D();
    game.SetCameraPosition(new Vector3(0f, 36f, 0f));
    game.SetCameraRotation(new Vector3(0f, -90f, 0f));

    var cameraEntity = scene.Entities.FirstOrDefault(e => e.Get<CameraComponent>() != null);
    if (cameraEntity is null)
        cameraEntity = game.SceneSystem.SceneInstance.RootScene.Entities
            .First(e => e.Get<CameraComponent>() != null);

    var camera = cameraEntity.Get<CameraComponent>();
    camera.Projection = CameraProjectionMode.Orthographic;
    camera.OrthographicSize = 24f;

    var floor = game.Create3DPrimitive(PrimitiveModelType.Cube, new()
    {
        Size = new Vector3(world.Floor.W, 0.12f, world.Floor.H),
        Material = game.CreateMaterial(new Color(92, 86, 74)),
        IncludeCollider = false
    });
    floor.Transform.Position = new Vector3(world.Floor.CenterX, -0.06f, world.Floor.CenterY);
    floor.Scene = scene;

    foreach (var wall in world.Walls)
    {
        const float height = 2.2f;
        var block = game.Create3DPrimitive(PrimitiveModelType.Cube, new()
        {
            Size = new Vector3(wall.W, height, wall.H),
            Material = game.CreateMaterial(new Color(58, 54, 50)),
            IncludeCollider = false
        });
        block.Transform.Position = new Vector3(wall.CenterX, height * 0.5f, wall.CenterY);
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

    world.Tick((float)time.Elapsed.TotalSeconds, new Vec2(x, y));
    PlaceHero();
}

void PlaceHero()
{
    if (hero is null)
        return;
    hero.Transform.Position = new Vector3(world.Hero.X, 0.55f, world.Hero.Y);
}
