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
        // Hide all items
        for (int i = 0; i < hotbarSlots; i++)
        {
            if (itemObjects[i] != null)
            {
                itemObjects[i].SetActive(false);
            }
        }

        // Show selected item in hand
        if (handPosition != null && selectedSlot < hotbarSlots && itemObjects[selectedSlot] != null)
        {
            GameObject item = itemObjects[selectedSlot];
            item.SetActive(true);
            item.transform.SetParent(handPosition);
            item.transform.localPosition = Vector3.zero;
            item.transform.localRotation = Quaternion.identity;
            item.transform.localScale = Vector3.one * 5f;

            // Disable colliders
            foreach (Collider col in item.GetComponentsInChildren<Collider>())
            {
                col.enabled = false;
            }
        }
    }

    public bool AddItem(Sprite icon, Material mat, GameObject itemObject)
    {
        for (int i = 0; i < hotbarSlots; i++)
        {
            if (itemIcons[i] == null)
            {
                itemIcons[i] = icon;
                itemMaterials[i] = mat;
                itemObjects[i] = itemObject;

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
        if (selectedSlot >= hotbarSlots) return;

        GameObject item = itemObjects[selectedSlot];
        if (item == null) return;

        // Unparent
        item.transform.SetParent(null);

        // Position in front of player
        item.transform.position = transform.position + transform.forward * 1.5f;
        item.transform.rotation = Quaternion.identity;

        // Enable colliders
        foreach (Collider col in item.GetComponentsInChildren<Collider>())
        {
            col.enabled = true;
            col.isTrigger = false;
        }

        // Add Rigidbody if it doesn't exist
        Rigidbody rb = item.GetComponent<Rigidbody>();
        if (rb == null)
            rb = item.AddComponent<Rigidbody>();

        rb.isKinematic = false;
        rb.useGravity = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        // Add pickup script
        if (!item.GetComponent<WorldPickup>())
            item.AddComponent<WorldPickup>();

        // Remove from inventory
        itemObjects[selectedSlot] = null;

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