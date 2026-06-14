using System;
using System.Collections.Generic;

/// <summary>
/// A single node in the research tree.
/// Loaded from Resources/Data/research_tree.json via ResearchCollection.
/// </summary>
[Serializable]
public class ResearchNode
{
    /// <summary>Unique identifier, e.g. "particle_cannons_1".</summary>
    public string id;

    /// <summary>Display name shown on the node.</summary>
    public string displayName;

    /// <summary>Full description shown in the detail panel.</summary>
    public string description;

    /// <summary>Category label: "Weapons", "Engines", "Crew", etc.</summary>
    public string category;

    /// <summary>Research point cost to unlock.</summary>
    public int cost;

    /// <summary>IDs of nodes that must be unlocked before this one.</summary>
    public string[] prerequisites;

    /// <summary>
    /// Normalised position in the tree canvas (0–1 on each axis).
    /// (0,0) = bottom-left, (1,1) = top-right.
    /// </summary>
    public float posX;
    public float posY;
}

/// <summary>
/// Root wrapper for research_tree.json — required by JsonUtility.
/// </summary>
[Serializable]
public class ResearchCollection
{
    public ResearchNode[] nodes;

    public static ResearchCollection LoadFromResources()
    {
        var asset = UnityEngine.Resources.Load<UnityEngine.TextAsset>("Data/research_tree");
        if (asset == null)
        {
            UnityEngine.Debug.LogWarning("[ResearchCollection] research_tree.json not found in Resources/Data/.");
            return null;
        }
        return UnityEngine.JsonUtility.FromJson<ResearchCollection>(asset.text);
    }

    public ResearchNode GetNode(string id)
    {
        if (nodes == null) return null;
        foreach (var n in nodes)
            if (n.id == id) return n;
        return null;
    }
}
