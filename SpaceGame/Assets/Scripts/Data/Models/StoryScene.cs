using System;

/// <summary>
/// A single frame within a story interlude.
///
/// JSON fields:
///   id            - Unique integer within this interlude (e.g. 1, 2, 3…).
///   imageResource - Path relative to a Resources folder, no extension
///                   (e.g. "Interludes/Images/sol_departure").
///                   Leave empty or null for a solid-colour placeholder.
///   text          - Caption shown at the bottom of the screen.
///   nextSceneId   - id of the scene to show when the player taps.
///                   0 means the interlude is complete (return to game).
/// </summary>
[Serializable]
public class StoryScene
{
    /// <summary>Unique identifier within the parent interlude.</summary>
    public int id;

    /// <summary>
    /// Resources-relative path to a Texture2D, no extension.
    /// Empty or null → show a placeholder panel instead.
    /// </summary>
    public string imageResource;

    /// <summary>Caption displayed at the bottom of the screen.</summary>
    public string text;

    /// <summary>
    /// id of the next scene to display when the player taps.
    /// 0 means this is the final scene — the interlude ends and the
    /// onComplete callback is invoked.
    /// </summary>
    public int nextSceneId;
}
