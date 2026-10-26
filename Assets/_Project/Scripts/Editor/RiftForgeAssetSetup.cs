using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace RiftForge.Editor
{
    /// <summary>
    /// Creates the small set of runtime materials that must exist as assets so native players keep
    /// their shader variants (additive particles, emissive lit). Safe to run in batchmode:
    /// Unity.exe -batchmode -nographics -projectPath ... -executeMethod RiftForge.Editor.RiftForgeAssetSetup.CreateRuntimeAssets -quit
    /// </summary>
    public static class RiftForgeAssetSetup
    {
        public const string SoftDotPath = "Assets/_Project/Art/rift_soft_dot.png";
        public const string ParticleMaterialPath = "Assets/_Project/Resources/RiftParticleAdd.mat";
        public const string GlowMaterialPath = "Assets/_Project/Resources/RiftURPLitGlow.mat";
        private const string LitMaterialPath = "Assets/_Project/Resources/RiftURPLit.mat";

        [MenuItem("Rift Forge/Create Runtime FX Assets")]
        public static void CreateRuntimeAssets()
        {
            Texture2D dot = CreateSoftDot();

            Shader particles = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (particles == null) throw new System.InvalidOperationException("URP Particles/Unlit shader missing.");
            Material particle = LoadOrCreate(ParticleMaterialPath, particles);
            particle.shader = particles;
            particle.SetFloat("_Surface", 1f);
            particle.SetFloat("_Blend", 2f); // Additive
            particle.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            particle.SetFloat("_DstBlend", (float)BlendMode.One);
            particle.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            particle.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
            particle.SetFloat("_ZWrite", 0f);
            particle.SetFloat("_Cull", (float)CullMode.Off);
            particle.SetOverrideTag("RenderType", "Transparent");
            particle.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            particle.renderQueue = (int)RenderQueue.Transparent;
            particle.SetTexture("_BaseMap", dot);
            particle.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(particle);

            Material lit = AssetDatabase.LoadAssetAtPath<Material>(LitMaterialPath);
            if (lit == null) throw new System.InvalidOperationException("RiftURPLit.mat missing.");
            Material glow = LoadOrCreate(GlowMaterialPath, lit.shader);
            glow.CopyPropertiesFromMaterial(lit);
            glow.EnableKeyword("_EMISSION");
            glow.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            glow.SetColor("_EmissionColor", Color.white);
            EditorUtility.SetDirty(glow);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("RIFT_FORGE_RUNTIME_ASSETS_OK");
        }

        private static Material LoadOrCreate(string path, Shader shader)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Texture2D CreateSoftDot()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f)) / (size / 2f);
                float a = Mathf.Clamp01(1f - d);
                a = a * a * (3f - 2f * a);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            texture.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(SoftDotPath));
            File.WriteAllBytes(SoftDotPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(SoftDotPath, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(SoftDotPath);
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(SoftDotPath);
        }
    }
}
