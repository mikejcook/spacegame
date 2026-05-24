using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// An ordered chain of <see cref="StoryScene"/> objects that play one after another.
///
/// Scenes are linked via their <c>nextSceneId</c> field — not by array position —
/// so scenes can branch, loop, or appear in any order in the JSON.
///
/// How to use
/// ----------
/// 1. Create a JSON file in Assets/Resources/Interludes/ (e.g. new_game_intro.json).
/// 2. Load it at runtime:
///    <code>
///    var interlude = StoryInterlude.LoadFromResources("Interludes/new_game_intro");
///    storyInterludeController.Play(interlude, OnInterludeComplete);
///    </code>
///
/// JSON structure example
/// ----------------------
/// {
///   "interludeId": "new_game_intro",
///   "startSceneId": 1,
///   "scenes": [
///     { "id": 1, "imageResource": "Interludes/Images/scene_01", "text": "Year 2387…", "nextSceneId": 2 },
///     { "id": 2, "imageResource": "",                          "text": "Prepare for departure.", "nextSceneId": 0 }
///   ]
/// }
/// </summary>
[Serializable]
public class StoryInterlude
{
    // -----------------------------------------------------------------------
    // Data fields  (must be public for JsonUtility)
    // -----------------------------------------------------------------------

    /// <summary>Human-readable identifier, e.g. "new_game_intro".</summary>
    public string interludeId;

    /// <summary>id of the first scene to display.</summary>
    public int startSceneId;

    /// <summary>All scenes in this interlude (order in the list doesn't matter).</summary>
    public List<StoryScene> scenes;

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Returns the scene with the given <paramref name="id"/>, or <c>null</c> if none exists.
    /// </summary>
    public StoryScene GetScene(int id)
    {
        if (scenes == null) return null;
        return scenes.Find(s => s.id == id);
    }

    // -----------------------------------------------------------------------
    // Factory
    // -----------------------------------------------------------------------

    /// <summary>
    /// Loads and deserialises a StoryInterlude from a JSON TextAsset in Resources.
    /// </summary>
    /// <param name="resourcePath">
    /// Path relative to any Resources folder, without extension.
    /// Example: <c>"Interludes/new_game_intro"</c>
    /// </param>
    /// <returns>The deserialised interlude, or <c>null</c> on failure.</returns>
    public static StoryInterlude LoadFromResources(string resourcePath)
    {
        var asset = Resources.Load<TextAsset>(resourcePath);
        if (asset == null)
        {
            Debug.LogWarning($"[StoryInterlude] Could not find TextAsset at Resources/{resourcePath}");
            return null;
        }

        var interlude = JsonUtility.FromJson<StoryInterlude>(asset.text);
        if (interlude == null)
            Debug.LogWarning($"[StoryInterlude] Failed to parse JSON at Resources/{resourcePath}");

        return interlude;
    }
}
