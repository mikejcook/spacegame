#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Linq;

/// <summary>
/// Scans Assets/Textures/Planets/, sets all PNGs to Sprite import mode,
/// and writes the results into PlanetLibrary.asset at Assets/Resources/.
///
/// Run via:  Star Captain → Build Planet Library
///
/// ── What it does ──────────────────────────────────────────────────────────
///
///   1. Finds every .png in Assets/Textures/Planets/
///   2. Sets TextureType = Sprite (single-sprite mode) on any that aren't already
///   3. Splits into planet sprites (everything else) and star sprites (Sun_* prefix)
///   4. Sorts each group alphabetically by key (filename stem)
///   5. Creates or updates Assets/Resources/PlanetLibrary.asset in-place
///      (preserves GUID so no scene references break)
///
/// ── Re-running ────────────────────────────────────────────────────────────
///
///   Safe to run repeatedly. The asset is updated in-place, not deleted/recreated.
/// </summary>
public static class PlanetLibraryBuilder
{
    private const string TexturesFolder = "Assets/Textures/Planets";
    private const string OutputAssetPath = "Assets/Resources/PlanetLibrary.asset";

    [MenuItem("Star Captain/Build Planet Library")]
    public static void Build()
    {
        // ── Ensure the Resources folder exists ────────────────────────────
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");

        // ── Ensure the Textures/Planets folder exists ─────────────────────
        if (!AssetDatabase.IsValidFolder("Assets/Textures"))
            AssetDatabase.CreateFolder("Assets", "Textures");
        if (!AssetDatabase.IsValidFolder(TexturesFolder))
            AssetDatabase.CreateFolder("Assets/Textures", "Planets");

        // ── Find all PNGs ─────────────────────────────────────────────────
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { TexturesFolder });
        if (guids.Length == 0)
        {
            EditorUtility.DisplayDialog(
                "Planet Library Builder",
                $"No textures found in {TexturesFolder}.\n\n" +
                "Copy the planet PNGs there first, then re-run.",
                "OK");
            return;
        }

        var allPaths = guids
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => p.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => Path.GetFileNameWithoutExtension(p))
            .ToArray();

        // ── Ensure all textures are imported as Sprites ───────────────────
        bool reimported = false;
        foreach (var path in allPaths)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType        = TextureImporterType.Sprite;
                importer.spriteImportMode   = SpriteImportMode.Single;
                importer.mipmapEnabled      = false;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
                reimported = true;
            }
        }

        // Refresh the asset database after any reimports
        if (reimported) AssetDatabase.Refresh();

        // ── Load sprites and separate stars from planets ──────────────────
        var planetSprites = new System.Collections.Generic.List<Sprite>();
        var planetKeys    = new System.Collections.Generic.List<string>();
        var starSprites   = new System.Collections.Generic.List<Sprite>();
        var starKeys      = new System.Collections.Generic.List<string>();

        foreach (var path in allPaths)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                Debug.LogWarning($"[PlanetLibraryBuilder] Could not load sprite at {path} — skipping.");
                continue;
            }

            string key = Path.GetFileNameWithoutExtension(path);

            if (key.StartsWith("Sun_", System.StringComparison.OrdinalIgnoreCase))
            {
                starSprites.Add(sprite);
                starKeys.Add(key);
            }
            else
            {
                planetSprites.Add(sprite);
                planetKeys.Add(key);
            }
        }

        // ── Create or update the ScriptableObject in-place ───────────────
        var library = AssetDatabase.LoadAssetAtPath<PlanetLibrary>(OutputAssetPath);
        if (library == null)
        {
            library = ScriptableObject.CreateInstance<PlanetLibrary>();
            AssetDatabase.CreateAsset(library, OutputAssetPath);
        }

        library.PlanetSprites = planetSprites.ToArray();
        library.PlanetKeys    = planetKeys.ToArray();
        library.StarSprites   = starSprites.ToArray();
        library.StarKeys      = starKeys.ToArray();

        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Planet Library Built",
            $"Planet Library updated.\n\n" +
            $"  {planetSprites.Count} planet sprites\n" +
            $"  {starSprites.Count} star sprites\n\n" +
            $"Asset saved to:\n  {OutputAssetPath}",
            "OK");

        Debug.Log($"[PlanetLibraryBuilder] Done. {planetSprites.Count} planets, {starSprites.Count} stars.");
    }
}
#endif
