using UnityEngine;
using UnityEngine.EventSystems;

public class DroppingBehaviour : MonoBehaviour, IDropHandler
{

    private Canvas canvas;
    private RectTransform rect;

    

    public void Awake()
    {
        canvas = GetComponentInParent<Canvas>();
        rect = GetComponent<RectTransform>();
        Debug.Log("Tag: " + tag);
    }

    
    public void OnDrop(PointerEventData eventData)
    {
        // get the center of the collision area
        float x = rect.anchoredPosition.x / canvas.scaleFactor;
        float y = rect.anchoredPosition.y /  canvas.scaleFactor;

        if (eventData.pointerDrag.GetComponent<RectTransform>().tag == tag)
        {
            eventData.pointerDrag.GetComponent<RectTransform>().anchoredPosition = new Vector2(x, y);
            eventData.pointerDrag.GetComponent<DraggingBehaviour>().locked = true;
            Debug.Log("LOCKED");

        }

        Debug.Log("Dropped");

    }



}
