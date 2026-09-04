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

        CastleWorld _world;
        Transform _hero;
        Material _blockMaterial;

        void Start()
        {
            _world = new CastleWorld();

            EnsureCamera();
            EnsureSun();

            CreateBlock(_world.Floor, 0f, FloorHeight, new Color(0.36f, 0.34f, 0.29f), "Floor", castsShadow: false);
            for (var i = 0; i < _world.Walls.Count; i++)
                CreateBlock(_world.Walls[i], 0f, WallHeight, new Color(0.23f, 0.21f, 0.20f), "Wall " + i, castsShadow: true);

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
        }

        void EnsureCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera");
                cam = go.AddComponent<Camera>();
            }

            cam.orthographic = true;
            cam.orthographicSize = 12f;
            cam.transform.position = new Vector3(0f, 30f, 0f);
            cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // straight down
            cam.backgroundColor = new Color(0.13f, 0.15f, 0.17f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 60f;
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

        void CreateBlock(Aabb box, float baseY, float height, Color color, string name, bool castsShadow)
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
        }

        GameObject CreateHero()
        {
            var go = new GameObject("Hero");
            go.transform.SetParent(transform, false);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // lie flat, facing the top-down camera

            var renderer = go.AddComponent<SpriteRenderer>();
            var size = CastleWorld.HeroHalf * 2f;
            renderer.sprite = Sprite.Create(
                CreateHeroTexture(),
                new Rect(0f, 0f, HeroPixels.Width, HeroPixels.Height),
                new Vector2(0.5f, 0.5f),
                HeroPixels.Width / size);
            // A flat cutout casts an ugly rectangular blob under directional light; it still
            // sits correctly inside the walls' shadows, which is the effect that matters here.
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            return go;
        }

        Texture2D CreateHeroTexture()
        {
            var tex = new Texture2D(HeroPixels.Width, HeroPixels.Height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            var argb = HeroPixels.CreateArgb();
            var colors = new Color32[argb.Length];
            for (var i = 0; i < argb.Length; i++)
            {
                var srcY = i / HeroPixels.Width;
                var x = i % HeroPixels.Width;
                var dstY = HeroPixels.Height - 1 - srcY;
                var p = argb[i];
                colors[dstY * HeroPixels.Width + x] = new Color32(
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
