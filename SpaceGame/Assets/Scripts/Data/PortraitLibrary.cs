using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject that holds references to the first N portrait textures
/// and their corresponding filenames. Populated via the editor menu item:
///   Star Captain -> Build Portrait Library
///
/// Assign the generated asset (Assets/Resources/PortraitLibrary.asset)
/// to the PortraitPickerPanel's "Portrait Library" field in the Inspector.
/// </summary>
[CreateAssetMenu(fileName = "PortraitLibrary", menuName = "Star Captain/Portrait Library")]
public class PortraitLibrary : ScriptableObject
{
    [Tooltip("Portrait textures (populated by the editor builder tool)")]
    public Texture2D[] Portraits;

    [Tooltip("Filename for each portrait, e.g. '1.png' (parallel array with Portraits)")]
    public string[] FileNames;

    public int Count => Portraits != null ? Portraits.Length : 0;

    public bool IsValid => Portraits != null && FileNames != null
                        && Portraits.Length == FileNames.Length
                        && Portraits.Length > 0;

    /// <summary>Returns a random filename from the library.</summary>
    public string RandomFileName()
    {
        if (!IsValid) return string.Empty;
        return FileNames[Random.Range(0, FileNames.Length)];
    }

    /// <summary>Returns a random index from the library.</summary>
    public int RandomIndex()
    {
        if (!IsValid) return 0;
        return Random.Range(0, Portraits.Length);
    }

    /// <summary>Looks up the index for a given filename (-1 if not found).</summary>
    public int IndexOf(string fileName)
    {
        if (!IsValid) return -1;
        for (int i = 0; i < FileNames.Length; i++)
            if (FileNames[i] == fileName) return i;
        return -1;
    }

    /// <summary>
    /// Returns a random portrait filename that matches <paramref name="gender"/> and
    /// is not already in <paramref name="usedFileNames"/>.
    ///
    /// Gender matching:
    ///   Male   → filenames starting with "male_"
    ///   Female → filenames starting with "female_"
    ///   NonBinary / None → any filename
    ///
    /// Falls back to any unused portrait if no gender-matched one is available,
    /// then to any portrait at all if every portrait is already used.
    /// </summary>
    public string RandomFileNameForGender(Gender gender, HashSet<string> usedFileNames = null)
    {
        if (!IsValid) return string.Empty;
        usedFileNames ??= new HashSet<string>();

        string prefix = gender == Gender.Male   ? "male_"
                      : gender == Gender.Female ? "female_"
                      : null;   // NonBinary / None → any portrait

        // Build candidate list: gender-matched and unused
        var candidates = new List<string>();
        foreach (var fn in FileNames)
        {
            if (usedFileNames.Contains(fn)) continue;
            if (prefix == null || fn.StartsWith(prefix))
                candidates.Add(fn);
        }

        if (candidates.Count > 0)
            return candidates[Random.Range(0, candidates.Count)];

        // Fallback 1: any unused portrait (ignoring gender)
        var anyUnused = new List<string>();
        foreach (var fn in FileNames)
            if (!usedFileNames.Contains(fn))
                anyUnused.Add(fn);

        if (anyUnused.Count > 0)
            return anyUnused[Random.Range(0, anyUnused.Count)];

        // Fallback 2: pool exhausted — just pick gender-matched (allow duplicates)
        var genderMatched = new List<string>();
        foreach (var fn in FileNames)
            if (prefix == null || fn.StartsWith(prefix))
                genderMatched.Add(fn);

        if (genderMatched.Count > 0)
            return genderMatched[Random.Range(0, genderMatched.Count)];

        return FileNames[Random.Range(0, FileNames.Length)];
    }
}
