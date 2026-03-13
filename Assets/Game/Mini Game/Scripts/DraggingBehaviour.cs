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
    private RectTransform _parentRect;
    private CanvasGroup _canvasGroup;

    [Header("Status")]
    public bool locked = false;
    public void Awake()
    {  
        _canvas = GetComponentInParent<Canvas>();
        _rect = GetComponent<RectTransform>();
        _parentRect = _rect != null ? _rect.parent as RectTransform : null;
        _canvasGroup = GetComponent<CanvasGroup>();
       
    }

   void Update()
    {
        if (locked && _canvasGroup != null)
        {  
            _canvasGroup.alpha = 1f;
            _canvasGroup.blocksRaycasts = false; // need to change when unlocked
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (locked) return;

       if (_canvasGroup != null)
       {
           _canvasGroup.alpha = 0.6f;
           _canvasGroup.blocksRaycasts = false;
       }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (locked || _rect == null) return;

        if (_parentRect == null)
            _parentRect = _rect.parent as RectTransform;

        float canvasScale = (_canvas != null && _canvas.scaleFactor > 0f) ? _canvas.scaleFactor : 1f;
        Vector2 nextPosition = _rect.anchoredPosition + (eventData.delta / canvasScale);
        _rect.anchoredPosition = ClampAnchoredPositionWithinParent(nextPosition);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (locked) return;

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 1f;
            _canvasGroup.blocksRaycasts = true;
        }
    }

    private Vector2 ClampAnchoredPositionWithinParent(Vector2 desiredPosition)
    {
        if (_rect == null || _parentRect == null)
            return desiredPosition;

        Rect parentBounds = _parentRect.rect;
        Vector2 size = _rect.rect.size;
        Vector3 scale = _rect.localScale;
        float width = Mathf.Abs(size.x * scale.x);
        float height = Mathf.Abs(size.y * scale.y);

        float leftPadding = width * _rect.pivot.x;
        float rightPadding = width * (1f - _rect.pivot.x);
        float bottomPadding = height * _rect.pivot.y;
        float topPadding = height * (1f - _rect.pivot.y);

        float anchorCenterX = (_rect.anchorMin.x + _rect.anchorMax.x) * 0.5f;
        float anchorCenterY = (_rect.anchorMin.y + _rect.anchorMax.y) * 0.5f;
        float anchorReferenceX = Mathf.Lerp(parentBounds.xMin, parentBounds.xMax, anchorCenterX);
        float anchorReferenceY = Mathf.Lerp(parentBounds.yMin, parentBounds.yMax, anchorCenterY);

        float minX = (parentBounds.xMin + leftPadding) - anchorReferenceX;
        float maxX = (parentBounds.xMax - rightPadding) - anchorReferenceX;
        float minY = (parentBounds.yMin + bottomPadding) - anchorReferenceY;
        float maxY = (parentBounds.yMax - topPadding) - anchorReferenceY;

        float x = minX <= maxX ? Mathf.Clamp(desiredPosition.x, minX, maxX) : 0.5f * (minX + maxX);
        float y = minY <= maxY ? Mathf.Clamp(desiredPosition.y, minY, maxY) : 0.5f * (minY + maxY);
        return new Vector2(x, y);
    }
}