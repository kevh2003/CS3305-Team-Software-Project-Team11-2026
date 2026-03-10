using System.Net.Sockets;
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
      
    }

    public void Update() // move this to on trigger
    {
         if(canvas.enabled) {
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
        float x = rect.anchoredPosition.x / canvas.scaleFactor;
        float y = rect.anchoredPosition.y /  canvas.scaleFactor;

        if (eventData.pointerDrag.GetComponent<RectTransform>().tag == tag)
        {
            eventData.pointerDrag.GetComponent<RectTransform>().anchoredPosition = new Vector2(x, y);
            eventData.pointerDrag.GetComponent<DraggingBehaviour>().locked = true;
           

        }

        
    }



}
