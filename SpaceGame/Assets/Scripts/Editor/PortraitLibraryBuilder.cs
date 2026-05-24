#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Linq;

/// <summary>
/// Editor utility that scans the BIG PORTRAITS PACK folder, picks the first 50
/// portraits (sorted numerically), and writes them into a PortraitLibrary
/// ScriptableObject at Assets/Resources/PortraitLibrary.asset.
///
/// Run via:  Star Captain -> Build Portrait Library
/// </summary>
public static class PortraitLibraryBuilder
{
    private const string PortraitsFolder  = "Assets/BIG PORTRAITS PACK (by Batareya)/PORTRAITS";
    private const string OutputAssetPath  = "Assets/Resources/PortraitLibrary.asset";
    private const int    MaxPortraits     = 50;

    [MenuItem("Star Captain/Build Portrait Library")]
    public static void Build()
    {
        // 1. Ensure the Resources folder exists
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");

        // 2. Find all textures in the portraits folder
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { PortraitsFolder });

        if (guids.Length == 0)
        {
            EditorUtility.DisplayDialog(
                "Portrait Library Builder",
                $"No textures found in:\n{PortraitsFolder}\n\nMake sure the BIG PORTRAITS PACK is imported.",
                "OK");
            return;
        }

        // 3. Filter to .png only, sort numerically by filename, take first N
        var sortedPaths = guids
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => p.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
            .OrderBy(p =>
            {
                string stem = Path.GetFileNameWithoutExtension(p);
                return int.TryParse(stem, out int n) ? n : int.MaxValue;
            })
            .Take(MaxPortraits)
            .ToArray();

        // 4. Load textures
        var textures  = new Texture2D[sortedPaths.Length];
        var fileNames = new string[sortedPaths.Length];

        for (int i = 0; i < sortedPaths.Length; i++)
        {
            textures[i]  = AssetDatabase.LoadAssetAtPath<Texture2D>(sortedPaths[i]);
            fileNames[i] = Path.GetFileName(sortedPaths[i]);
        }

        // 5. Create or update the ScriptableObject
        var library = AssetDatabase.LoadAssetAtPath<PortraitLibrary>(OutputAssetPath);
        if (library == null)
        {
            library = ScriptableObject.CreateInstance<PortraitLibrary>();
            AssetDatabase.CreateAsset(library, OutputAssetPath);
        }

        library.Portraits = textures;
        library.FileNames = fileNames;

        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[PortraitLibraryBuilder] Built library with {library.Count} portraits at {OutputAssetPath}");
        EditorUtility.DisplayDialog(
            "Portrait Library Built",
            $"Successfully loaded {library.Count} portraits.\n\nAsset saved to:\n{OutputAssetPath}\n\nAssign it to your PortraitPickerPanel in the Inspector.",
            "OK");
    }

    [MenuItem("Star Captain/Build Portrait Library", validate = true)]
    public static bool BuildValidate()
    {
        return AssetDatabase.IsValidFolder(PortraitsFolder);
    }
}
#endif
