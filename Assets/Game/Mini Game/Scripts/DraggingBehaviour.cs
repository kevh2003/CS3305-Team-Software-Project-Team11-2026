using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;

/*
Usage - Attach this script to the UI element you want to be draggable.
Ensure there is a CanvasGroup component attached to the same UI element for proper functionality.
Match the tag on this object with thet collision area

TODO - Press to enter and exit 
TODO - Mark when completed
TODO - change the model when it is done
*/

public class DraggingBehaviour : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{

    private Canvas canvas;
    private RectTransform rect;
    private CanvasGroup canvasGroup;
    public bool locked = false;
    public void Awake()
    {  
        canvas = GetComponentInParent<Canvas>();
        rect = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
       
    }

   void Update()
    {
        if (locked)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = false;
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (locked) return;
        canvasGroup.alpha = 0.6f;
        canvasGroup.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (locked) return;
        rect.anchoredPosition += eventData.delta / canvas.scaleFactor;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (locked) return;
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
    }

}
