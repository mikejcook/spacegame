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
}
