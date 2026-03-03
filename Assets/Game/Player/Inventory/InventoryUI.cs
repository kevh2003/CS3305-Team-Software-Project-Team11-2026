using System.Collections;
using UnityEngine;

public class InventoryUI : MonoBehaviour
{
    [SerializeField] private GameObject[] slotHighlights;

    private PlayerInventory inventory;
    private Transform handPosition;
    private Transform dropPosition;

    void Awake()
    {
        inventory = GetComponent<PlayerInventory>();
        StartCoroutine(SetupHandAndDropPositions());
    }

    IEnumerator SetupHandAndDropPositions()
    {
        // IMPORTANT:
        // Do NOT use Camera.main here (unreliable with multiple players / ParrelSync / builds).
        // Always use THIS player's camera. - kev
        Camera cam = null;

        while (cam == null || !cam.gameObject.activeInHierarchy)
        {
            cam = GetComponentInChildren<Camera>(true);
            yield return null;
        }

        // Hand anchor under this player's camera
        var hp = cam.transform.Find("HandPosition");
        if (hp == null)
        {
            var go = new GameObject("HandPosition");
            hp = go.transform;
            hp.SetParent(cam.transform, false);
            hp.localPosition = new Vector3(0.25f, -0.25f, 0.5f);
            hp.localRotation = Quaternion.identity;
        }

        // Drop anchor in front of this player's camera
        var dp = cam.transform.Find("DropPosition");
        if (dp == null)
        {
            var go = new GameObject("DropPosition");
            dp = go.transform;
            dp.SetParent(cam.transform, false);
            dp.localPosition = new Vector3(0f, -0.2f, 1.5f);
            dp.localRotation = Quaternion.identity;
        }

        handPosition = hp;
        dropPosition = dp;

        // Push anchors into PlayerInventory so it never falls back to player-root parenting
        if (inventory != null)
        {
            inventory.SetAnchors(handPosition, dropPosition);
        }
    }

    public void SetSelectedSlot(int index)
    {
        if (slotHighlights == null) return;

        for (int i = 0; i < slotHighlights.Length; i++)
        {
            if (slotHighlights[i] != null)
                slotHighlights[i].SetActive(i == index);
        }
    }
}