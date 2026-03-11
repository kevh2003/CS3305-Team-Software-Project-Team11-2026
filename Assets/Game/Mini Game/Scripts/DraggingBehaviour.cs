using UnityEngine;
using UnityEngine.EventSystems;

/*
Usage - Attach this script to the UI element you want to be draggable.
Ensure there is a CanvasGroup component attached to the same UI element for proper functionality.
Match the tag on this object with thet collision area



*/

public class DraggingBehaviour : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{

    [Header("Canvas")]
    private Canvas _canvas;
    private RectTransform _rect;
    private CanvasGroup _canvasGroup;

    [Header("Status")]
    public bool locked = false;
    public void Awake()
    {  
        _canvas = GetComponentInParent<Canvas>();
        _rect = GetComponent<RectTransform>();
        _canvasGroup = GetComponent<CanvasGroup>();
       
    }

   void Update()
    {
        if (locked)
        {  
            _canvasGroup.alpha = 1f;
            _canvasGroup.blocksRaycasts = false; // need to change when unlocked
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (locked) return;
       _canvasGroup.alpha = 0.6f;
       _canvasGroup.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (locked) return;
        _rect.anchoredPosition += eventData.delta / _canvas.scaleFactor;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (locked) return;
        _canvasGroup.alpha = 1f;
        _canvasGroup.blocksRaycasts = true;
    }

}
