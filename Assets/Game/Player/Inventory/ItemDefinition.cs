using UnityEngine;

[CreateAssetMenu(menuName = "Items/Item Definition")]
// ScriptableObject describing a pickup/held item and its visual assets.
public class ItemDefinition : ScriptableObject
{
    public int itemId;              
    public string displayName;

    public GameObject worldPrefab;    // spawned/dropped in world
    public GameObject handPrefab;     // shown in hand
    public Sprite icon;               // UI hotbar icon
}