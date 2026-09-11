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
        const float TorchHeight = 1.6f;
        // Tints the torch lights, the hero's own light and the ambient floor, so unlit ground
        // reads as the same firelight everything else is bathed in, only fainter.
        static readonly Color TorchColor = new Color(1f, 0.63f, 0.31f);

        // The hero carries his own light: a torch's colour, far broader and far flatter. Range is
        // the only knob for the shape of it -- built-in point lights attenuate as
        // 1/(1 + 25(d/range)^2), a function of the ratio alone, so a longer range does not merely
        // push the edge out, it slows the whole falloff. At 10x a torch he keeps roughly two thirds
        // of his light ten units out and a third at twenty, where 5x left 0.46 and 0.15. Past about
        // 15x the curve is flat enough across the view that it stops reading as a pool travelling
        // with him and starts reading as the world simply being brighter.
        //
        // It costs nothing at his feet: attenuation is 1 at the light itself whatever the range, so
        // the sum there stays 1.75 with the ambient floor. That headroom is the point -- gamma space
        // clips the sum of the lights at white and the brightest ground clips past about 2.1, and a
        // hero bright enough to saturate the ground he stands on would leave a torch beside him
        // nothing to add, making "next to a torch" look identical to "alone".
        const float HeroLightRange = CastleWorld.TorchRange * 10f;   // 70
        const float HeroLightIntensity = 1.4f;                        // a torch is 1.5
        const float HeroLightHeight = 1.2f;                           // about his own height

        CastleWorld _world;
        CameraFollow _follow;
        Camera _camera;
        Transform _hero;
        Transform _heroLight;
        Material _blockMaterial;
        Texture2D[] _surfaceTextures;
        Sprite _flameSprite;

        void Start()
        {
            _world = new CastleWorld();
            _follow = new CameraFollow(_world.Bounds, _world.Hero);

            EnsureCamera();
            EnsureNight();

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

            // Built-in forward rendering lights each object with only pixelLightCount per-pixel
            // lights (4 by default), and the grass is a single object -- without this most torch
            // pools would never show. The + 1 is the hero's own light, which would otherwise drop
            // out of the ground wherever enough torches were already in range of it.
            QualitySettings.pixelLightCount = Mathf.Max(QualitySettings.pixelLightCount, _world.Torches.Count + 1);
            for (var i = 0; i < _world.Torches.Count; i++)
                CreateTorch(_world.Torches[i], "Torch " + i);

            _hero = CreateHero().transform;
            _heroLight = CreateHeroLight().transform;
        }

        // A torch: a dark post, a bright flame on top, and a warm point light at flame height. The
        // light casts shadows -- that is what stops it reaching through a wall into the next room.
        // The post and flame cast none, or the torch would throw a dark streak across its own light.
        void CreateTorch(Vec2 at, string name)
        {
            const float post = 0.25f;
            const float flame = 0.3f;
            CreateBlock(new Aabb(at.X - post * 0.5f, at.Y - post * 0.5f, post, post),
                0f, TorchHeight, new Color(0.27f, 0.2f, 0.13f), name + " post", castsShadow: false);
            // The flame is a flat sprite, like the hero: the default sprite material is unlit, so it
            // glows at night. A lit cube came out black -- its own light sits level with its top
            // face -- and the Standard shader's emission variant can be stripped from builds.
            var flameGo = new GameObject(name + " flame");
            flameGo.transform.SetParent(transform, false);
            flameGo.transform.position = new Vector3(at.X, TorchHeight + flame, at.Y);
            flameGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // lie flat, facing the camera
            flameGo.transform.localScale = new Vector3(flame, flame, 1f);
            var flameRenderer = flameGo.AddComponent<SpriteRenderer>();
            flameRenderer.sprite = FlameSprite();
            flameRenderer.color = new Color(1f, 0.8f, 0.35f);
            flameRenderer.shadowCastingMode = ShadowCastingMode.Off;
            flameRenderer.receiveShadows = false;

            var go = new GameObject(name + " light");
            go.transform.SetParent(transform, false);
            // Sim Y maps onto world Z, as for everything else here.
            go.transform.position = new Vector3(at.X, TorchHeight + 0.3f, at.Y);
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = CastleWorld.TorchRange;
            light.color = TorchColor;
            // The only real light in the scene; everything else is the ambient floor.
            light.intensity = CastleWorld.TorchIntensity;
            light.shadows = LightShadows.Hard;
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
            _heroLight.position = new Vector3(_world.Hero.X, HeroLightHeight, _world.Hero.Y);

            // orthographicSize is the HALF height, so the visible height is twice it.
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
            _camera.backgroundColor = Color.black;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 60f;
        }

        // Night: no sun, no moon, and nothing from the sky -- the Standard shader would otherwise
        // pick up ambient and reflected light from the scene's default skybox and lift everything
        // towards daylight. What replaces it is a flat floor of CastleWorld.AmbientLight in the
        // torch colour, so ground the torches never reach stays walkable instead of going black.
        static void EnsureNight()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = TorchColor * CastleWorld.AmbientLight;
            RenderSettings.reflectionIntensity = 0f;
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

        // One white unit square, shared by every flame and tinted per renderer.
        Sprite FlameSprite()
        {
            if (_flameSprite != null)
                return _flameSprite;
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            _flameSprite = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            return _flameSprite;
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
            // The hero sits outside the lighting: the default sprite material is unlit, so no
            // light or shadow changes his colour, and he casts no shadow of his own -- which is
            // also what stops his own light throwing a streak of him across the ground.
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go;
        }

        // A sibling of the hero rather than a child of him: his GameObject is rotated flat to face
        // the camera, and a child's offset is expressed in that rotated space, so lifting the light
        // to head height would push it out along world Z instead of up. Parented to the unrotated
        // root, it is placed each frame in Update, as the hero already is.
        GameObject CreateHeroLight()
        {
            var go = new GameObject("Hero light");
            go.transform.SetParent(transform, false);
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = HeroLightRange;
            light.color = TorchColor;
            light.intensity = HeroLightIntensity;
            // Shadows, as the torches have: it is what stops him lighting the next room through a
            // wall. His is the one shadow map that genuinely redraws, since he moves.
            light.shadows = LightShadows.Hard;
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
