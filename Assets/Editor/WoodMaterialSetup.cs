using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Configure les textures Poly Haven "white_oak_veneer" et branche le tout sur Wood.mat (URP Lit).
/// Menu : Outils > Configurer le matériau Wood.
/// </summary>
public static class WoodMaterialSetup
{
    private const string TextureFolder = "Assets/Materials/Wood Texture";
    private const string Prefix = "white_oak_veneer";
    private const string MaterialPath = "Assets/Materials/Wood.mat";
    private const int MaxSize = 2048;

    private static string DiffusePath => $"{TextureFolder}/{Prefix}_diff_4k.jpg";
    private static string NormalPath => $"{TextureFolder}/{Prefix}_nor_gl_4k.png";
    private static string RoughPath => $"{TextureFolder}/{Prefix}_rough_4k.jpg";
    private static string AoPath => $"{TextureFolder}/{Prefix}_ao_4k.jpg";
    private static string SmoothnessPath => $"{TextureFolder}/{Prefix}_metal_smooth.png";

    [MenuItem("Outils/Configurer le matériau Wood")]
    public static void Run()
    {
        // Réglages d'import : couleur en sRGB, tout le reste en linéaire.
        ConfigureImporter(DiffusePath, TextureImporterType.Default, true);
        ConfigureImporter(NormalPath, TextureImporterType.NormalMap, false);
        ConfigureImporter(RoughPath, TextureImporterType.Default, false);
        ConfigureImporter(AoPath, TextureImporterType.Default, false);

        GenerateSmoothnessMap();
        ConfigureImporter(SmoothnessPath, TextureImporterType.Default, false, alphaFromInput: true);

        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            // Le matériau n'existe pas (ou plus) : on le crée en URP Lit.
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("Shader URP Lit introuvable.");
                return;
            }

            material = new Material(shader);
            AssetDatabase.CreateAsset(material, MaterialPath);
        }

        material.SetTexture("_BaseMap", Load(DiffusePath));
        material.SetTexture("_BumpMap", Load(NormalPath));
        material.SetTexture("_MetallicGlossMap", Load(SmoothnessPath));
        material.SetTexture("_OcclusionMap", Load(AoPath));

        // Bois : non métallique. Le smoothness vient de l'alpha de la Metallic Map.
        material.SetFloat("_Metallic", 0f);
        material.SetFloat("_Smoothness", 1f);
        material.SetFloat("_SmoothnessTextureChannel", 0f);
        material.EnableKeyword("_NORMALMAP");
        material.EnableKeyword("_METALLICSPECGLOSSMAP");
        material.EnableKeyword("_OCCLUSIONMAP");

        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
        Debug.Log("Wood.mat configuré (base, normal, metallic/smoothness, occlusion).");
    }

    private static Texture2D Load(string path) => AssetDatabase.LoadAssetAtPath<Texture2D>(path);

    private static void ConfigureImporter(string path, TextureImporterType type, bool sRGB, bool alphaFromInput = false)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            Debug.LogError($"Texture introuvable : {path}");
            return;
        }

        importer.textureType = type;
        importer.sRGBTexture = sRGB;
        importer.maxTextureSize = MaxSize;
        importer.mipmapEnabled = true;
        importer.alphaSource = alphaFromInput ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
        importer.alphaIsTransparency = false;
        importer.SaveAndReimport();
    }

    /// <summary>
    /// Génère une texture R = metallic (0), A = smoothness (1 - roughness).
    /// Lit le fichier brut pour ne pas dépendre de l'option Read/Write de l'import.
    /// </summary>
    private static void GenerateSmoothnessMap()
    {
        var bytes = File.ReadAllBytes(RoughPath);
        var rough = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
        if (!rough.LoadImage(bytes))
        {
            Debug.LogError($"Lecture impossible : {RoughPath}");
            return;
        }

        var pixels = rough.GetPixels32();
        for (var i = 0; i < pixels.Length; i++)
        {
            var smoothness = (byte)(255 - pixels[i].r);
            pixels[i] = new Color32(0, 0, 0, smoothness);
        }

        var result = new Texture2D(rough.width, rough.height, TextureFormat.RGBA32, false, true);
        result.SetPixels32(pixels);
        result.Apply();

        File.WriteAllBytes(SmoothnessPath, result.EncodeToPNG());
        Object.DestroyImmediate(rough);
        Object.DestroyImmediate(result);

        AssetDatabase.ImportAsset(SmoothnessPath, ImportAssetOptions.ForceUpdate);
    }
}
