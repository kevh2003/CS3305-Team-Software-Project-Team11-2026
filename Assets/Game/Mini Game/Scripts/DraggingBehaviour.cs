using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;

/*
Usage - Attach this script to the UI element you want to be draggable.
Ensure there is a CanvasGroup component attached to the same UI element for proper functionality.
Match the tag on this object with thet collision area

TODO - Start and finish the mini game detection
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

        // TODO - move this to a when the wifi game is interacted with, need to have a button to detect for quit
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (locked)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = false;
        }
        
        // test for now to lock the player
        NetworkPlayer player = GetComponentInParent<NetworkPlayer>();
        player.lookSensitivity = 0;
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
