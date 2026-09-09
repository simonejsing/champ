using Champ.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace Champ.Unity
{
    public sealed class CastleView : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindObjectOfType<CastleView>() != null)
                return;
            var go = new GameObject("CastleView");
            go.AddComponent<CastleView>();
        }

        const float WallHeight = 2.2f;
        const float FloorHeight = 0.15f;
        const float CameraHeight = 30f;
        const float SurfaceStep = 0.02f;

        CastleWorld _world;
        CameraFollow _follow;
        Camera _camera;
        Transform _hero;
        Material _blockMaterial;
        Texture2D[] _surfaceTextures;

        void Start()
        {
            _world = new CastleWorld();
            _follow = new CameraFollow(_world.Bounds, _world.Hero);

            EnsureCamera();
            EnsureSun();

            // Surfaces are ordered back-to-front; lifting patch i by its index keeps
            // elevation in step with paint order so overlapping ground never z-fights.
            // The topmost patch lands at FloorHeight, where the lone floor slab used to be.
            for (var i = 0; i < _world.Surfaces.Count; i++)
            {
                var surface = _world.Surfaces[i];
                var block = CreateBlock(
                    surface.Box,
                    (i - (_world.Surfaces.Count - 1)) * SurfaceStep,
                    FloorHeight,
                    Color.white,
                    "Surface " + i,
                    castsShadow: false);

                // Tile the shared ground texels across the patch. White tint lets the
                // texture's own colours through unchanged.
                var material = block.GetComponent<Renderer>().material;
                material.mainTexture = SurfaceTexture(surface.Kind);
                material.mainTextureScale = new Vector2(
                    surface.Box.W / SurfacePixels.WorldSize,
                    surface.Box.H / SurfacePixels.WorldSize);
            }

            for (var i = 0; i < _world.Walls.Count; i++)
            {
                var box = _world.Walls[i];
                var wall = CreateBlock(box, 0f, WallHeight, Color.white, "Wall " + i, castsShadow: true);
                var wallMaterial = wall.GetComponent<Renderer>().material;
                wallMaterial.mainTexture = SurfaceTexture(SurfaceKind.Wall);
                wallMaterial.mainTextureScale = new Vector2(
                    box.W / SurfacePixels.WorldSize,
                    box.H / SurfacePixels.WorldSize);
            }

            _hero = CreateHero().transform;
        }

        void Update()
        {
            var x = 0f;
            var y = 0f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) x -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) x += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) y -= 1f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) y += 1f;

            _world.Tick(Time.deltaTime, new Vec2(x, y));
            // Sim stays 2D (X, Y); the view maps sim Y onto world Z, sim X onto world X,
            // and reserves world Y for height so a top-down camera can see 3D depth/shadows.
            _hero.position = new Vector3(_world.Hero.X, FloorHeight + 0.01f, _world.Hero.Y);

            // orthographicSize is the HALF height in Unity, unlike Stride's OrthographicSize.
            var viewHeight = _camera.orthographicSize * 2f;
            _follow.Update(_world.Hero, viewHeight * _camera.aspect, viewHeight, Time.deltaTime);
            _camera.transform.position =
                new Vector3(_follow.Center.X, CameraHeight, _follow.Center.Y);
        }

        void EnsureCamera()
        {
            _camera = Camera.main;
            if (_camera == null)
            {
                var go = new GameObject("Main Camera");
                _camera = go.AddComponent<Camera>();
            }

            _camera.orthographic = true;
            _camera.orthographicSize = 12f;
            // Start on the hero so the follow camera has nothing to catch up on frame one.
            _camera.transform.position = new Vector3(_follow.Center.X, CameraHeight, _follow.Center.Y);
            _camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // straight down
            _camera.backgroundColor = new Color(0.13f, 0.15f, 0.17f);
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 60f;
        }

        void EnsureSun()
        {
            var go = new GameObject("Sun");
            go.transform.SetParent(transform, false);
            go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.88f);
            light.intensity = 1.15f;
            light.shadows = LightShadows.Soft;
        }

        Texture2D SurfaceTexture(SurfaceKind kind)
        {
            _surfaceTextures ??= new Texture2D[System.Enum.GetValues(typeof(SurfaceKind)).Length];
            var i = (int)kind;
            if (_surfaceTextures[i] == null)
                _surfaceTextures[i] = CreateTexture(
                    SurfacePixels.CreateArgb(kind),
                    SurfacePixels.Size,
                    SurfacePixels.Size,
                    TextureWrapMode.Repeat);
            return _surfaceTextures[i];
        }

        GameObject CreateBlock(Aabb box, float baseY, float height, Color color, string name, bool castsShadow)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            go.transform.localScale = new Vector3(box.W, height, box.H);
            go.transform.position = new Vector3(box.CenterX, baseY + height * 0.5f, box.CenterY);

            // Resources/BlockMaterial.mat (see AssetGenerator) keeps the Standard shader from
            // being stripped out of the build -- a bare CreatePrimitive default material has no
            // serialized reference anywhere, so it renders as missing-shader magenta once built.
            _blockMaterial ??= Resources.Load<Material>("BlockMaterial");
            var renderer = go.GetComponent<Renderer>();
            renderer.material = new Material(_blockMaterial) { color = color };
            renderer.shadowCastingMode = castsShadow ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            return go;
        }

        GameObject CreateHero()
        {
            var go = new GameObject("Hero");
            go.transform.SetParent(transform, false);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // lie flat, facing the top-down camera

            var renderer = go.AddComponent<SpriteRenderer>();
            var size = CastleWorld.HeroHalf * 2f;
            renderer.sprite = Sprite.Create(
                CreateTexture(
                    HeroPixels.CreateArgb(),
                    HeroPixels.Width,
                    HeroPixels.Height,
                    TextureWrapMode.Clamp),
                new Rect(0f, 0f, HeroPixels.Width, HeroPixels.Height),
                new Vector2(0.5f, 0.5f),
                HeroPixels.Width / size);
            // A flat cutout casts an ugly rectangular blob under directional light; it still
            // sits correctly inside the walls' shadows, which is the effect that matters here.
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            return go;
        }

        static Texture2D CreateTexture(uint[] argb, int width, int height, TextureWrapMode wrap)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = wrap
            };

            var colors = new Color32[argb.Length];
            for (var i = 0; i < argb.Length; i++)
            {
                // Unity textures are bottom-up; the shared pixels are top-down.
                var srcY = i / width;
                var x = i % width;
                var dstY = height - 1 - srcY;
                var p = argb[i];
                colors[dstY * width + x] = new Color32(
                    (byte)(p >> 16),
                    (byte)(p >> 8),
                    (byte)p,
                    (byte)(p >> 24));
            }

            tex.SetPixels32(colors);
            tex.Apply();
            return tex;
        }
    }
}
