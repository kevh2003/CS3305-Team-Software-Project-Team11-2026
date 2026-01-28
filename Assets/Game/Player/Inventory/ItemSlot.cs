using UnityEngine;
using UnityEngine.EventSystems;

public class ItemSlot : MonoBehaviour, IPointerClickHandler
{
    public int slotIndex;
    public InventorySystem inventorySystem;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (inventorySystem != null)
        {
            inventorySystem.OnSlotClicked(slotIndex);
        }
    }
}