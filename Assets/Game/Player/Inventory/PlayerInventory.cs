using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Unity.Netcode;

public class PlayerInventory : NetworkBehaviour
{
    [Header("Hotbar Settings")]
    public int hotbarSlots = 2;

    private Sprite[] itemIcons;
    private Material[] itemMaterials;
    private GameObject[] itemObjects;
    private int selectedSlot = 0;

    [HideInInspector] public GameObject hotbarPanel;
    [HideInInspector] public Image[] hotbarSlotImages;
    [HideInInspector] public Transform handPosition;
    [HideInInspector] public Transform dropPosition;

    private PlayerInputActions inputActions;

    void Awake()
    {
        itemIcons = new Sprite[hotbarSlots];
        itemMaterials = new Material[hotbarSlots];
        itemObjects = new GameObject[hotbarSlots];
        inputActions = new PlayerInputActions();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        inputActions.Enable();
        SetupInputCallbacks();
    }

    void SetupInputCallbacks()
    {
        if (inputActions == null) return;

        inputActions.Player.HotbarSlot0.performed += ctx => SelectSlot(0);
        inputActions.Player.HotbarSlot1.performed += ctx => SelectSlot(1);
        inputActions.Player.DropItem.performed += ctx => DropItem(selectedSlot);
    }

    void Update()
    {
        if (!IsOwner) return;

        // Fallback Q key
        if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame)
        {
            DropItem(selectedSlot);
        }
    }

    public void SelectSlot(int index)
    {
        if (index < 0 || index >= hotbarSlots) return;

        selectedSlot = index;
        UpdateHotbarOutlines();
        UpdateHandDisplay();
    }

    void UpdateHotbarOutlines()
    {
        if (hotbarSlotImages == null) return;

        for (int i = 0; i < hotbarSlotImages.Length; i++)
        {
            if (hotbarSlotImages[i] == null) continue;

            Outline outline = hotbarSlotImages[i].GetComponent<Outline>();
            if (outline == null)
            {
                outline = hotbarSlotImages[i].gameObject.AddComponent<Outline>();
                outline.effectColor = new Color32(139, 0, 0, 255);
                outline.effectDistance = new Vector2(3, 3);
            }
            outline.enabled = (i == selectedSlot);
        }
    }

    void UpdateHandDisplay()
    {
        // Hide all items (they stay in world, just hidden)
        for (int i = 0; i < hotbarSlots; i++)
        {
            if (itemObjects[i] != null)
            {
                itemObjects[i].SetActive(false);
            }
        }

        // For now, im not showing items in hand
        // Just keeping them hidden when in inventory
    }

    public bool AddItem(Sprite icon, Material mat, GameObject itemObject)
    {
        for (int i = 0; i < hotbarSlots; i++)
        {
            if (itemIcons[i] == null)
            {
                // Store the original scale before we modify anything
                Vector3 originalScale = itemObject.transform.localScale;

                itemIcons[i] = icon;
                itemMaterials[i] = mat;
                itemObjects[i] = itemObject;

                // Store original scale in a component so we can retrieve it later
                ItemData data = itemObject.GetComponent<ItemData>();
                if (data == null)
                {
                    data = itemObject.AddComponent<ItemData>();
                }
                data.originalScale = originalScale;
                data.originalIcon = icon;
                data.originalMaterial = mat;

                if (hotbarSlotImages[i] != null)
                {
                    hotbarSlotImages[i].sprite = icon;
                    hotbarSlotImages[i].color = Color.white;
                }

                if (i == selectedSlot)
                {
                    UpdateHandDisplay();
                }

                return true;
            }
        }

        return false;
    }

    public void RemoveItem(int index)
    {
        if (index < 0 || index >= hotbarSlots) return;

        itemIcons[index] = null;
        itemMaterials[index] = null;
        itemObjects[index] = null;

        if (hotbarSlotImages[index] != null)
        {
            hotbarSlotImages[index].sprite = null;
            hotbarSlotImages[index].color = new Color(1, 1, 1, 0.3f);
        }

        if (index == selectedSlot)
        {
            UpdateHandDisplay();
        }
    }

    void DropItem(int index)
    {
        if (index < 0 || index >= hotbarSlots) return;

        GameObject item = itemObjects[index];
        if (item == null) return;

        // Get stored data
        ItemData data = item.GetComponent<ItemData>();

        // Unparent from hand
        item.transform.SetParent(null);

        // Position in front of player, slightly up so it doesn't clip through floor
        Vector3 dropPos = transform.position + (transform.forward * 1.5f);
        dropPos.y = transform.position.y + 0.5f; // Waist height
        item.transform.position = dropPos;
        item.transform.rotation = Quaternion.identity;

        // Restore original scale
        item.transform.localScale = Vector3.one * 10f;

        // Re-enable colliders
        foreach (Collider col in item.GetComponentsInChildren<Collider>())
        {
            col.enabled = true;
            col.isTrigger = false;
        }

        // Ensure Rigidbody exists and is configured
        Rigidbody rb = item.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = item.AddComponent<Rigidbody>();
        }
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        // Ensure WorldPickup exists and has correct data
        WorldPickup pickup = item.GetComponent<WorldPickup>();
        if (pickup == null)
        {
            pickup = item.AddComponent<WorldPickup>();
        }
        // Restore the data
        if (data != null)
        {
            pickup.itemIcon = data.originalIcon;
            pickup.itemMaterial = data.originalMaterial;
        }

        // Make item visible again
        item.SetActive(true);

        // Remove from inventory
        itemObjects[index] = null;
        itemIcons[index] = null;
        itemMaterials[index] = null;

        // Update UI
        if (hotbarSlotImages[index] != null)
        {
            hotbarSlotImages[index].sprite = null;
            hotbarSlotImages[index].color = new Color(1, 1, 1, 0.3f);
        }

        UpdateHandDisplay();
    }


    void OnDestroy()
    {
        if (inputActions != null)
        {
            inputActions.Disable();
        }
    }
}