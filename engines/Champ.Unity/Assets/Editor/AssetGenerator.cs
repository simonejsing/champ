using System.IO;
using UnityEditor;
using UnityEngine;

namespace Champ.Unity.Editor
{
    /// <summary>
    /// CastleView builds its whole scene from code, including per-wall lit materials it paints
    /// via a cloned Material.color. A build only keeps a shader if some serialized asset
    /// references it -- a runtime-only Shader.Find("Standard") material has no such reference,
    /// so Standard gets stripped from Windows builds and every block renders magenta (missing
    /// shader). Resources/BlockMaterial.mat is a real serialized reference to Standard, and
    /// anything under Resources/ is always kept, so this regenerates it if it's ever missing.
    /// </summary>
    public static class AssetGenerator
    {
        const string MaterialPath = "Assets/Resources/BlockMaterial.mat";

        [MenuItem("Champ/Regenerate Runtime Materials")]
        public static void RegenerateBlockMaterial()
        {
            Directory.CreateDirectory("Assets/Resources");
            var shader = Shader.Find("Standard");
            if (shader == null)
            {
                Debug.LogError("Standard shader not found; cannot regenerate BlockMaterial.");
                return;
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            else
            {
                material.shader = shader;
                EditorUtility.SetDirty(material);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Wrote {MaterialPath}");
        }
    }
}
