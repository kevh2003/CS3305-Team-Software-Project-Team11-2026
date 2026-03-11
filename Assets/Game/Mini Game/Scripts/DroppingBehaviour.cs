using System.Net.Sockets;
using UnityEngine;
using UnityEngine.EventSystems;

public class DroppingBehaviour : MonoBehaviour, IDropHandler
{
    [Header("Player")]
    private Canvas _canvas;

    [Header("Collision Area")]
    private RectTransform _rect;


    public void Awake()
    {
        _canvas = GetComponentInParent<Canvas>();
        _rect = GetComponent<RectTransform>();
      
    }

    public void Update()
    {
         if(_canvas.enabled) {

            // show the cursor 
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // lock the player camera
            NetworkPlayer player = GetComponentInParent<NetworkPlayer>();
            player.lookSensitivity = 0;

        } else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // lock the player camera
            NetworkPlayer player = GetComponentInParent<NetworkPlayer>();
            player.lookSensitivity = 0;

        }

    }

    
    public void OnDrop(PointerEventData eventData)
    {
        // get the center of the collision area
        float x = _rect.anchoredPosition.x / _canvas.scaleFactor;
        float y = _rect.anchoredPosition.y /  _canvas.scaleFactor;

        if (eventData.pointerDrag.GetComponent<RectTransform>().tag == tag)
        {
            eventData.pointerDrag.GetComponent<RectTransform>().anchoredPosition = new Vector2(x, y);
            eventData.pointerDrag.GetComponent<DraggingBehaviour>().locked = true;
           

        }
        
    }

}
