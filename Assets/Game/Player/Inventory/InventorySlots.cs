using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Handles click events on inventory slots for drag & drop.
/// </summary>
public class InventorySlot : MonoBehaviour, IPointerClickHandler
{
    public int slotIndex;
    public PlayerInventory inventory;

    private static int draggedFromSlot = -1;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (inventory == null) return;

        if (draggedFromSlot == -1)
        {
            // First click - pick up item
            if (inventory.GetItem(slotIndex) != null)
            {
                draggedFromSlot = slotIndex;
                Debug.Log($"Picked up item from slot {slotIndex}");
            }
        }
        else
        {
            // Second click - place/swap item
            inventory.SwapItems(draggedFromSlot, slotIndex);
            draggedFromSlot = -1;
            Debug.Log($"Swapped items");
        }
    }
}