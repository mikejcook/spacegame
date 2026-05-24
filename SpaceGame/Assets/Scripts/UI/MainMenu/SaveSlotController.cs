using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controls a single save-game slot in the Load Game panel.
/// Attach to the SaveSlot prefab. Wire references in the prefab's Inspector.
/// </summary>
public class SaveSlotController : MonoBehaviour
{
    [SerializeField] private TMP_Text captainNameText;
    [SerializeField] private TMP_Text shipNameText;
    [SerializeField] private TMP_Text lastSavedText;
    [SerializeField] private TMP_Text playTimeText;
    [SerializeField] private TMP_Text creditsText;
    [SerializeField] private Button   loadButton;

    /// <summary>
    /// Populate this slot with save data and wire the load action.
    /// Called by MainMenuController after instantiating the prefab.
    /// </summary>
    public void Initialize(SaveGame save, Action onLoad)
    {
        if (captainNameText) captainNameText.text = $"Captain {save.CaptainName}";
        if (shipNameText)    shipNameText.text    = save.ShipName ?? "Unknown Vessel";
        if (lastSavedText)   lastSavedText.text   = $"Saved {save.LastSavedAt:MMM d, yyyy  h:mm tt}";
        if (creditsText)     creditsText.text     = $"{save.Credits:N0} credits";

        if (playTimeText)
        {
            var span = TimeSpan.FromSeconds(save.PlayTime);
            playTimeText.text = span.TotalHours >= 1
                ? $"{(int)span.TotalHours}h {span.Minutes}m"
                : $"{span.Minutes}m";
        }

        loadButton?.onClick.AddListener(() => onLoad?.Invoke());
    }
}
