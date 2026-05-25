using System;
using System.Collections.Generic;

/// <summary>
/// An ordered chain of <see cref="StoryScene"/> objects that play one after another.
///
/// Scenes are linked via their <c>nextSceneId</c> field — not by array position —
/// so scenes can branch, loop, or appear in any order in the JSON.
///
/// Interludes are stored as items in the shared <c>Resources/Interludes/stories.json</c>
/// collection file.  Do not load them directly — use <see cref="StoryCollection"/> instead:
///
/// <code>
/// var stories   = StoryCollection.LoadFromResources();
/// var interlude = stories?.GetInterlude(Constants.Interludes.NewGameIntroId);
/// storyInterludeController.Play(interlude, OnInterludeComplete);
/// </code>
///
/// JSON structure (one item in the "interludes" array of stories.json)
/// -------------------------------------------------------------------
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

}
