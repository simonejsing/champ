using Champ.Sim;
using UnityEngine;

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

        CastleWorld _world;
        Transform _hero;
        Texture2D _white;

        void Start()
        {
            _world = new CastleWorld();
            _white = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _white.SetPixel(0, 0, Color.white);
            _white.Apply();

            EnsureCamera();
            CreateFilled(_world.Floor, new Color(0.36f, 0.34f, 0.29f), "Floor", 0);
            for (var i = 0; i < _world.Walls.Count; i++)
                CreateFilled(_world.Walls[i], new Color(0.23f, 0.21f, 0.20f), "Wall " + i, 1);

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
            _hero.position = new Vector3(_world.Hero.X, _world.Hero.Y, -1f);
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
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.backgroundColor = new Color(0.13f, 0.15f, 0.17f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 50f;
        }

        void CreateFilled(Aabb box, Color color, string name, int sorting)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(box.CenterX, box.CenterY, 0f);
            go.transform.localScale = new Vector3(box.W, box.H, 1f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = Sprite.Create(
                _white,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f);
            renderer.color = color;
            renderer.sortingOrder = sorting;
        }

        GameObject CreateHero()
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

            var go = new GameObject("Hero");
            go.transform.SetParent(transform, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            var size = CastleWorld.HeroHalf * 2f;
            renderer.sprite = Sprite.Create(
                tex,
                new Rect(0f, 0f, HeroPixels.Width, HeroPixels.Height),
                new Vector2(0.5f, 0.5f),
                HeroPixels.Width / size);
            renderer.sortingOrder = 2;
            return go;
        }
    }
}
