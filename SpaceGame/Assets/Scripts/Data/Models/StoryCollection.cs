using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Top-level container for all story interludes in the game.
///
/// A single JSON file (<c>Resources/Interludes/stories.json</c>) holds every
/// interlude as items in the <c>interludes</c> array.  This lets us keep all
/// story content in one place while still addressing each interlude by its
/// <c>interludeId</c> string.
///
/// JSON structure
/// --------------
/// {
///   "interludes": [
///     {
///       "interludeId": "new_game_intro",
///       "startSceneId": 1,
///       "scenes": [ ... ]
///     },
///     { ... }
///   ]
/// }
///
/// Usage
/// -----
/// <code>
/// var stories = StoryCollection.LoadFromResources();
/// var interlude = stories?.GetInterlude(Constants.Interludes.NewGameIntroId);
/// storyInterludeController.Play(interlude, OnComplete);
/// </code>
/// </summary>
[Serializable]
public class StoryCollection
{
    // -----------------------------------------------------------------------
    // Data fields  (must be public for JsonUtility)
    // -----------------------------------------------------------------------

    /// <summary>All interludes defined in the story file.</summary>
    public List<StoryInterlude> interludes;

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Returns the interlude whose <c>interludeId</c> matches <paramref name="id"/>,
    /// or <c>null</c> if none is found.
    /// </summary>
    public StoryInterlude GetInterlude(string id)
    {
        if (interludes == null) return null;
        return interludes.Find(i => i.interludeId == id);
    }

    // -----------------------------------------------------------------------
    // Factory
    // -----------------------------------------------------------------------

    /// <summary>
    /// Loads and deserialises the story collection from
    /// <c>Resources/Interludes/stories.json</c>.
    /// </summary>
    /// <returns>The deserialised collection, or <c>null</c> on failure.</returns>
    public static StoryCollection LoadFromResources()
    {
        const string resourcePath = "Interludes/stories";
        var asset = Resources.Load<TextAsset>(resourcePath);
        if (asset == null)
        {
            Debug.LogWarning($"[StoryCollection] Could not find TextAsset at Resources/{resourcePath}");
            return null;
        }

        var collection = JsonUtility.FromJson<StoryCollection>(asset.text);
        if (collection == null)
            Debug.LogWarning($"[StoryCollection] Failed to parse JSON at Resources/{resourcePath}");

        return collection;
    }
}
