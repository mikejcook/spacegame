#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Linq;

/// <summary>
/// Editor utility that scans Assets/Resources/Portraits/ and writes the
/// found portraits into the PortraitLibrary ScriptableObject at
/// Assets/Resources/PortraitLibrary.asset.
///
/// Files are sorted males first (male_1 … male_N) then females (female_1 … female_N). All found portraits are included.
///
/// Run via:  Star Captain -> Build Portrait Library
/// </summary>
public static class PortraitLibraryBuilder
{
    private const string PortraitsFolder = "Assets/Resources/Portraits";
    private const string OutputAssetPath = "Assets/Resources/PortraitLibrary.asset";

    [MenuItem("Star Captain/Build Portrait Library")]
    public static void Build()
    {
        // 1. Ensure Resources folder exists
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");

        // 2. Find all textures in Portraits
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { PortraitsFolder });

        if (guids.Length == 0)
        {
            EditorUtility.DisplayDialog(
                "Portrait Library Builder",
                $"No textures found in:\n{PortraitsFolder}\n\nMake sure Assets/Resources/Portraits/ exists and contains PNG files.",
                "OK");
            return;
        }

        // 3. Filter to .png, sort males first (numerically), then females
        var sortedPaths = guids
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => p.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => Path.GetFileNameWithoutExtension(p).StartsWith("male_") ? 0 : 1)
            .ThenBy(p =>
            {
                string stem = Path.GetFileNameWithoutExtension(p);
                int    idx  = stem.LastIndexOf('_');
                return idx >= 0 && int.TryParse(stem.Substring(idx + 1), out int n) ? n : 0;
            })
            .ToArray();

        // 4. Load textures + record filenames
        var textures  = new Texture2D[sortedPaths.Length];
        var fileNames = new string[sortedPaths.Length];

        for (int i = 0; i < sortedPaths.Length; i++)
        {
            textures[i]  = AssetDatabase.LoadAssetAtPath<Texture2D>(sortedPaths[i]);
            fileNames[i] = Path.GetFileName(sortedPaths[i]);
        }

        // 5. Create or update the ScriptableObject.
        // Load the existing asset and update it in place — this preserves its GUID so no
        // scene references break. If the file genuinely doesn't exist yet, create it fresh.
        //
        // IMPORTANT: do NOT delete-then-recreate here. Deleting the asset assigns a new GUID
        // and silently breaks every scene that referenced the old one. If LoadAssetAtPath
        // returns null despite the file existing on disk, the Unity Library cache is stale
        // (usually caused by moving files outside the editor). Fix that by closing Unity and
        // deleting the Library folder — not by nuking the asset from builder code.
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
            $"Successfully loaded {library.Count} portraits from Portrait.\n\nAsset saved to:\n{OutputAssetPath}",
            "OK");
    }
}
#endif
