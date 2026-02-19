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
        
        Debug.Log("PlayerInventory: Input enabled and callbacks set up");
    }

    void SetupInputCallbacks()
    {
        if (inputActions == null)
        {
            Debug.LogError("PlayerInventory: inputActions is null");
            return;
        }

        inputActions.Player.HotbarSlot0.performed += ctx => SelectSlot(0);
        inputActions.Player.HotbarSlot1.performed += ctx => SelectSlot(1);
        inputActions.Player.DropItem.performed += ctx => OnDropItemPressed();
        // inputActions.Player.ScrollWheel.performed += ctx => HandleScrollWheel(ctx.ReadValue<float>());
        
        Debug.Log("PlayerInventory: All input callbacks registered");
    }

    void OnDropItemPressed()
    {
        Debug.Log($"Q PRESSED! Dropping item from slot {selectedSlot}");
        DropItem(selectedSlot);
    }

    void Update()
    {
        if (!IsOwner) return;
        
        // Fallback: Check for Q key directly
        if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame)
        {
            Debug.Log("Q key detected via Keyboard.current");
            DropItem(selectedSlot);
        }
    }

    void HandleScrollWheel(float scrollValue)
    {
        if (scrollValue > 0.1f)
        {
            SelectSlot((selectedSlot + 1) % hotbarSlots);
        }
        else if (scrollValue < -0.1f)
        {
            SelectSlot((selectedSlot - 1 + hotbarSlots) % hotbarSlots);
        }
    }

    public void SelectSlot(int index)
    {
        if (index < 0 || index >= hotbarSlots) return;

        selectedSlot = index;
        UpdateHotbarOutlines();
        UpdateHandDisplay();
        
        Debug.Log($"Selected slot {index}");
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
        if (handPosition != null)
        {
            foreach (Transform child in handPosition)
            {
                Destroy(child.gameObject);
            }
        }

        if (selectedSlot < hotbarSlots && itemMaterials[selectedSlot] != null)
        {
            if (handPosition != null)
            {
                GameObject handItem = GameObject.CreatePrimitive(PrimitiveType.Cube);
                handItem.transform.SetParent(handPosition);
                handItem.transform.localPosition = Vector3.zero;
                handItem.transform.localRotation = Quaternion.identity;
                handItem.transform.localScale = Vector3.one * 0.3f;

                Collider col = handItem.GetComponent<Collider>();
                if (col != null) Destroy(col);

                Renderer rend = handItem.GetComponent<Renderer>();
                if (rend != null)
                {
                    rend.material = itemMaterials[selectedSlot];
                }
            }
        }
    }

    public bool AddItem(Sprite icon, Material mat)
    {
        for (int i = 0; i < hotbarSlots; i++)
        {
            if (itemIcons[i] == null)
            {
                itemIcons[i] = icon;
                itemMaterials[i] = mat;

                if (hotbarSlotImages[i] != null)
                {
                    hotbarSlotImages[i].sprite = icon;
                    hotbarSlotImages[i].color = Color.white;
                }

                Debug.Log($"Added item to slot {i}");

                if (i == selectedSlot)
                {
                    UpdateHandDisplay();
                }

                return true;
            }
        }

        Debug.LogWarning("Hotbar full");
        return false;
    }

    public void RemoveItem(int index)
    {
        if (index < 0 || index >= hotbarSlots) return;
        
        itemIcons[index] = null;
        itemMaterials[index] = null;
        
        if (hotbarSlotImages[index] != null)
        {
            hotbarSlotImages[index].sprite = null;
            hotbarSlotImages[index].color = new Color(1, 1, 1, 0.3f);
        }
        
        if (index == selectedSlot)
        {
            UpdateHandDisplay();
        }
        
        Debug.Log($"Removed item from slot {index}");
    }

    void DropItem(int index)
    {
        Debug.Log($"DropItem called for index {index}");
        
        if (index < 0 || index >= hotbarSlots)
        {
            Debug.LogError($"Invalid index: {index}");
            return;
        }
        
        if (itemIcons[index] == null)
        {
            Debug.LogWarning($"No item in slot {index}");
            return;
        }
        
        if (dropPosition == null)
        {
            Debug.LogError("dropPosition is null");
            return;
        }

        Debug.Log($"Dropping item from slot {index}");

        GameObject dropped = GameObject.CreatePrimitive(PrimitiveType.Cube);
        dropped.transform.position = dropPosition.position + dropPosition.forward * 1.5f;
        dropped.transform.localScale = Vector3.one * 0.5f;

        if (itemMaterials[index] != null)
        {
            dropped.GetComponent<Renderer>().material = itemMaterials[index];
        }

        WorldPickup pickup = dropped.AddComponent<WorldPickup>();
        pickup.itemIcon = itemIcons[index];
        pickup.itemMaterial = itemMaterials[index];

        Rigidbody rb = dropped.AddComponent<Rigidbody>();
        rb.mass = 0.5f;

        Debug.Log("Item dropped successfully");

        RemoveItem(index);
    }

    void OnDestroy()
    {
        if (inputActions != null)
        {
            inputActions.Disable();
        }
    }
}